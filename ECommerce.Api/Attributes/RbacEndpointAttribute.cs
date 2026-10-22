namespace ECommerceRbac.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RbacEndpointAttribute : Attribute
{
    public string AuthMode { get; }
    public bool IsSystem { get; }

    public RbacEndpointAttribute(string authMode, bool isSystem = false)
    {
        AuthMode = authMode;
        IsSystem = isSystem;
    }
}