namespace Theon.AspNetCore;

/// <summary>The factory for schemas over uploaded files.</summary>
/// <remarks>
/// <para>
/// A second factory, beside <c>Theo</c>, and it exists because it has to. <c>Theo</c> lives in the core
/// package, which has no dependency on anything and is never going to acquire one;
/// <see cref="Microsoft.AspNetCore.Http.IFormFile"/> is an ASP.NET Core type. A factory method for it
/// cannot live where every other factory method lives, so it lives where the type does, named for the
/// thing it builds schemas about.
/// </para>
/// <para>
/// Not named <c>FormFile</c>, which would collide with
/// <see cref="Microsoft.AspNetCore.Http.FormFile"/> in any file that has both namespaces in scope —
/// which is every file that uses this.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// app.MapPost("/avatar", (IFormFile file) =&gt; Results.Ok())
///    .Validate(Upload.File()
///        .MaxSize(5 * 1024 * 1024, "Pick an image no larger than 5 MB.")
///        .ContentType("image/png", "image/jpeg")
///        .Extension(".png", ".jpg", ".jpeg"));
/// </code>
/// </example>
public static class Upload
{
    /// <summary>Starts a schema for one uploaded file.</summary>
    public static FormFileSchema File() => new();

    /// <summary>Starts a schema for the set of files in one request.</summary>
    public static FormFileCollectionSchema Files() => new();
}
