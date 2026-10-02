#!/bin/sh
set -eu

BUCKET="garage-local-media"
QUEUE="garage-photos"
DLQ="garage-photos-dlq"
REGION="eu-central-1"

# This job runs on every `docker compose up`, not just the first, so every
# step must be safe to repeat. The bucket is the one step that is not:
# creating a bucket you already own is an error, so ask first.
if awslocal s3api head-bucket --bucket "$BUCKET" 2>/dev/null; then
  echo "bucket $BUCKET already exists"
else
  echo "creating bucket $BUCKET"
  awslocal s3api create-bucket \
    --bucket "$BUCKET" \
    --create-bucket-configuration LocationConstraint="$REGION"
fi

# The browser PUTs from http://localhost:8080 to http://localhost:4566.
# Different port means different origin, so S3 needs CORS or the
# browser refuses the request before it is even sent.
cat > /tmp/cors.json <<'JSON'
{
  "CORSRules": [
    {
      "AllowedHeaders": ["*"],
      "AllowedMethods": ["PUT", "GET", "HEAD"],
      "AllowedOrigins": ["*"],
      "ExposeHeaders": ["ETag"],
      "MaxAgeSeconds": 3000
    }
  ]
}
JSON

awslocal s3api put-bucket-cors \
  --bucket "$BUCKET" \
  --cors-configuration file:///tmp/cors.json

echo "creating dead-letter queue $DLQ"
DLQ_URL=$(awslocal sqs create-queue --queue-name "$DLQ" \
  --query QueueUrl --output text)
DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url "$DLQ_URL" \
  --attribute-names QueueArn --query Attributes.QueueArn --output text)

echo "creating queue $QUEUE with redrive to $DLQ_ARN"
cat > /tmp/queue-attributes.json <<JSON
{
  "VisibilityTimeout": "60",
  "ReceiveMessageWaitTimeSeconds": "20",
  "RedrivePolicy": "{\"deadLetterTargetArn\":\"$DLQ_ARN\",\"maxReceiveCount\":\"3\"}"
}
JSON

QUEUE_URL=$(awslocal sqs create-queue --queue-name "$QUEUE" \
  --attributes file:///tmp/queue-attributes.json \
  --query QueueUrl --output text)
QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url "$QUEUE_URL" \
  --attribute-names QueueArn --query Attributes.QueueArn --output text)

echo "allowing S3 to send to $QUEUE_ARN"
cat > /tmp/queue-policy.json <<JSON
{
  "Policy": "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"Service\":\"s3.amazonaws.com\"},\"Action\":\"sqs:SendMessage\",\"Resource\":\"$QUEUE_ARN\"}]}"
}
JSON

awslocal sqs set-queue-attributes \
  --queue-url "$QUEUE_URL" \
  --attributes file:///tmp/queue-policy.json

echo "wiring s3:ObjectCreated on originals/ to the queue"
cat > /tmp/notification.json <<JSON
{
  "QueueConfigurations": [
    {
      "QueueArn": "$QUEUE_ARN",
      "Events": ["s3:ObjectCreated:*"],
      "Filter": {
        "Key": {
          "FilterRules": [
            { "Name": "prefix", "Value": "originals/" }
          ]
        }
      }
    }
  ]
}
JSON

awslocal s3api put-bucket-notification-configuration \
  --bucket "$BUCKET" \
  --notification-configuration file:///tmp/notification.json

echo "LOCALSTACK INIT COMPLETE"
