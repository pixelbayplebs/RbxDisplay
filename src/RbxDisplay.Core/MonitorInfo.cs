namespace RbxDisplay;

public sealed class MonitorInfo
{
    public string Device { get; set; } = "";
    public string Identity { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Primary { get; set; }

    public override string ToString()
    {
        return FriendlyName(Name);
    }

    public static string FriendlyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Monitor";
        string current = name.Trim();
        while (current.Length > 1 && current[^1] == ')')
        {
            int open = current.LastIndexOf('(');
            if (open <= 0)
                break;
            string head = current[..open].TrimEnd();
            if (head.Length == 0)
                break;
            current = head;
        }

        return current;
    }
}
