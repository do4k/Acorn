namespace Acorn.Extensions;

/// <summary>
///     Excludes a type from convention-based discovery by
///     <c>AddAllOfType&lt;T&gt;()</c>. Apply it to types that implement a scanned
///     contract but must be created by a factory registration instead — for
///     example a handler whose constructor takes dependencies the container does
///     not register as services.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class SkipAutoRegistrationAttribute : Attribute
{
}
