namespace Garage.Data;

public static class SnakeCase
{
    /// <summary>"AwaitingUpload" -> "awaiting_upload", "AspNetUsers" -> "asp_net_users".</summary>
    public static string From(string name)
    {
        var chars = new List<char>(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                chars.Add('_');
            }
            chars.Add(char.ToLowerInvariant(name[i]));
        }
        return new string(chars.ToArray());
    }
}
