using Microsoft.AspNetCore.Http;
using Theon.Errors;

namespace Theon.AspNetCore.Tests;

/// <summary>
/// Rules for uploaded files.
/// </summary>
/// <remarks>
/// The size is the only fact among them. The content type and the file name are what the client
/// claimed, which these rules check and which no amount of checking makes true; the test that pins
/// that is the one about a name and a type that disagree.
/// </remarks>
public class UploadSchemaTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FormFile Make(
        string name = "photo.png",
        string contentType = "image/png",
        long length = 1024) =>
        new(new MemoryStream(new byte[length]), 0, length, "upload", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

    private static FormFileCollection Collection(params IFormFile[] files)
    {
        var collection = new FormFileCollection();
        collection.AddRange(files);
        return collection;
    }

    [Fact]
    public void A_File_That_Satisfies_Every_Rule_Is_Accepted()
    {
        var schema = Upload.File()
            .MinSize(1)
            .MaxSize(5_000_000)
            .ContentType("image/png", "image/jpeg")
            .Extension(".png", ".jpg");

        Assert.True(schema.SafeParse(Make()).IsSuccess);
    }

    [Fact]
    public void A_File_Over_The_Size_Limit_Is_Reported_In_Bytes()
    {
        var result = Upload.File().MaxSize(1000).SafeParse(Make(length: 2000));

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooBig, error.Code);
        Assert.Equal(ValidationOrigin.Bytes, error.Info.Origin);
        Assert.Equal(1000L, error.Info.Maximum);
        Assert.Equal("Must be at most 1000 byte(s).", error.Message);
    }

    // The bound reported is the one declared, never the measurement that failed it, which is what the
    // error model says everywhere else. A sentence a person would rather read is one argument away.
    [Fact]
    public void A_Caller_Can_Say_It_In_Megabytes_Themselves()
    {
        var schema = Upload.File().MaxSize(5 * 1024 * 1024, "Pick an image no larger than 5 MB.");

        var result = schema.SafeParse(Make(length: 9_000_000));

        Assert.Equal("Pick an image no larger than 5 MB.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void An_Empty_File_Fails_A_Minimum_Size()
    {
        var result = Upload.File().MinSize(1).SafeParse(Make(length: 0));

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooSmall, error.Code);
        Assert.Equal(ValidationOrigin.Bytes, error.Info.Origin);
    }

    [Fact]
    public void A_Content_Type_Outside_The_Set_Is_Reported_With_The_Set()
    {
        var result = Upload.File()
            .ContentType("image/png", "image/jpeg")
            .SafeParse(Make(name: "doc.pdf", contentType: "application/pdf"));

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidFormat, error.Code);
        Assert.Equal("content_type", error.Info.Format);
        Assert.Equal("Must be one of these types: image/png, image/jpeg.", error.Message);
    }

    // A media type is case-insensitive by specification, and a section that declares a charset is still
    // declaring the type before the semicolon.
    [Theory]
    [InlineData("text/csv")]
    [InlineData("TEXT/CSV")]
    [InlineData("text/csv; charset=utf-8")]
    [InlineData("  text/csv ; charset=utf-8")]
    public void A_Content_Type_Is_Matched_On_The_Media_Type_Alone(string declared)
    {
        var schema = Upload.File().ContentType("text/csv");

        Assert.True(schema.SafeParse(Make(name: "rows.csv", contentType: declared)).IsSuccess);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData("png")]
    [InlineData("PNG")]
    public void An_Extension_Is_Accepted_With_Or_Without_The_Dot_And_In_Any_Case(string declared)
    {
        var schema = Upload.File().Extension(declared);

        Assert.True(schema.SafeParse(Make(name: "PHOTO.PnG")).IsSuccess);
    }

    [Fact]
    public void An_Extension_Outside_The_Set_Is_Reported_With_The_Set()
    {
        var result = Upload.File().Extension("png", "jpg").SafeParse(Make(name: "notes.txt"));

        var error = Assert.Single(result.Errors);
        Assert.Equal("file_extension", error.Info.Format);
        Assert.Equal("Must be one of these file types: .png, .jpg.", error.Message);
    }

    // The two rules check two different headers, so a file can satisfy one and fail the other. That is
    // the case that shows what these rules are and are not: nothing here reads a byte of the file.
    [Fact]
    public void A_Name_And_A_Type_That_Disagree_Each_Report_For_Themselves()
    {
        var result = Upload.File()
            .ContentType("image/png")
            .Extension(".png")
            .SafeParse(Make(name: "trojan.exe", contentType: "application/octet-stream"));

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(["content_type", "file_extension"], result.Errors.Select(e => e.Info.Format));
    }

    [Fact]
    public void A_Schema_Over_The_File_Name_Composes()
    {
        var schema = Upload.File().Named(Theo.String().MaxLength(8));

        Assert.True(schema.SafeParse(Make(name: "ok.png")).IsSuccess);

        var result = schema.SafeParse(Make(name: "an-extremely-long-name.png"));
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooBig, error.Code);
        Assert.Equal(0, error.Path.Segments.Length);
    }

    [Fact]
    public void A_Null_File_Is_Rejected_Rather_Than_Thrown_On()
    {
        var result = Upload.File().MaxSize(10).SafeParse(null!);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("A value is required.", error.Message);
    }

    [Fact]
    public void A_Rule_That_Accepts_Nothing_Is_Refused_At_Construction()
    {
        Assert.Throws<ArgumentException>(() => Upload.File().ContentType());
        Assert.Throws<ArgumentException>(() => Upload.File().Extension());
    }

    // Schemas are immutable, including against an array the caller kept a reference to.
    [Fact]
    public void Every_Rule_Returns_A_New_Schema()
    {
        var original = Upload.File();
        var narrowed = original.MaxSize(10);

        Assert.NotSame(original, narrowed);
        Assert.True(original.SafeParse(Make(length: 2000)).IsSuccess);
        Assert.False(narrowed.SafeParse(Make(length: 2000)).IsSuccess);
    }

    [Fact]
    public void An_Array_The_Caller_Changes_Afterwards_Does_Not_Change_The_Schema()
    {
        var types = new[] { "image/png" };
        var schema = Upload.File().ContentType(types);

        types[0] = "application/pdf";

        Assert.True(schema.SafeParse(Make(contentType: "image/png")).IsSuccess);
        Assert.False(schema.SafeParse(Make(contentType: "application/pdf")).IsSuccess);
    }

    [Fact]
    public void A_Set_Of_Files_Has_Count_Rules()
    {
        var schema = Upload.Files().MinCount(1).MaxCount(2);

        Assert.False(schema.SafeParse(Collection()).IsSuccess);
        Assert.True(schema.SafeParse(Collection(Make(), Make())).IsSuccess);
        Assert.False(schema.SafeParse(Collection(Make(), Make(), Make())).IsSuccess);
    }

    [Fact]
    public void A_Failing_File_Is_Reported_At_Its_Index()
    {
        var schema = Upload.Files().Each(Upload.File().MaxSize(100));

        var result = schema.SafeParse(Collection(Make(length: 10), Make(length: 500)));

        var error = Assert.Single(result.Errors);
        Assert.Equal("[1]", error.Path.ToString());
    }

    // The rule that only exists at this level: every file is under the per-file limit and the request
    // as a whole is not.
    [Fact]
    public void A_Total_Size_Budget_Catches_What_Per_File_Rules_Cannot()
    {
        var schema = Upload.Files()
            .Each(Upload.File().MaxSize(1000))
            .MaxTotalSize(1500);

        Assert.True(schema.SafeParse(Collection(Make(length: 700), Make(length: 700))).IsSuccess);

        var result = schema.SafeParse(Collection(Make(length: 900), Make(length: 900)));
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationOrigin.Bytes, error.Info.Origin);
        Assert.Equal(0, error.Path.Segments.Length);
    }

    [Fact]
    public void A_Null_Set_Of_Files_Is_Rejected_Rather_Than_Thrown_On()
    {
        var result = Upload.Files().MinCount(1).SafeParse(null!);

        Assert.Equal(ValidationErrorCode.InvalidType, Assert.Single(result.Errors).Code);
    }

    // Reading Length, ContentType and FileName is property access, and comparing them is done over
    // spans against arrays normalized when the schema was built. Nothing on the way through a valid
    // file has any reason to allocate, and this is the measurement rather than the assumption.
    [Fact]
    public void A_Valid_File_Costs_Nothing()
    {
        var schema = Upload.File()
            .MinSize(1)
            .MaxSize(5_000_000)
            .ContentType("image/png", "image/jpeg")
            .Extension(".png", ".jpg");

        var file = Make();

        // Warmed up first, so that what is measured is the parse and not the first call's one-time
        // costs.
        for (var i = 0; i < 100; i++)
        {
            schema.IsValid(file);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 100; i++)
        {
            schema.IsValid(file);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void A_Valid_Set_Of_Files_Costs_Nothing()
    {
        var schema = Upload.Files()
            .MinCount(1)
            .MaxCount(10)
            .MaxTotalSize(5_000_000)
            .Each(Upload.File().MaxSize(1_000_000));

        var files = Collection(Make(), Make(), Make());

        for (var i = 0; i < 100; i++)
        {
            schema.IsValid(files);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 100; i++)
        {
            schema.IsValid(files);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // Both of these are composite schemas, so asynchrony has to reach what is inside them. Without the
    // override an asynchronous rule on the file would refuse to run at all, which is the loud failure
    // decision 8 chose over a quiet one -- loud, and still wrong.
    [Fact]
    public async Task An_Asynchronous_Rule_On_A_File_Reaches_It()
    {
        var schema = Upload.File()
            .MaxSize(5_000_000)
            .RefineAsync(
                static (file, _) => ValueTask.FromResult(file.Length > 0),
                "The file is empty.");

        Assert.True((await schema.SafeParseAsync(Make(), cancellationToken: Token)).IsSuccess);

        var result = await schema.SafeParseAsync(Make(length: 0), cancellationToken: Token);
        Assert.Equal("The file is empty.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public async Task An_Asynchronous_Rule_Inside_Each_Reaches_Every_File()
    {
        var schema = Upload.Files()
            .MinCount(1)
            .Each(Upload.File().RefineAsync(
                static (file, _) => ValueTask.FromResult(file.Length > 0),
                "The file is empty."));

        Assert.True((await schema.SafeParseAsync(Collection(Make(), Make()), cancellationToken: Token)).IsSuccess);

        var result = await schema.SafeParseAsync(Collection(Make(), Make(length: 0)), cancellationToken: Token);
        var error = Assert.Single(result.Errors);
        Assert.Equal("[1]", error.Path.ToString());
        Assert.Equal("The file is empty.", error.Message);
    }

    [Fact]
    public async Task An_Asynchronous_Rule_On_The_File_Name_Reaches_It()
    {
        var schema = Upload.File().Named(Theo.String().RefineAsync(
            static (name, _) => ValueTask.FromResult(name.EndsWith(".png", StringComparison.Ordinal)),
            "Pick a PNG."));

        Assert.True((await schema.SafeParseAsync(Make(), cancellationToken: Token)).IsSuccess);

        var result = await schema.SafeParseAsync(Make(name: "notes.txt"), cancellationToken: Token);
        Assert.Equal("Pick a PNG.", Assert.Single(result.Errors).Message);
    }

    // The counts and the budget still report on the asynchronous path, which is the half of the
    // override that is easy to leave out.
    [Fact]
    public async Task The_Counts_And_The_Budget_Still_Report_Asynchronously()
    {
        var schema = Upload.Files().MinCount(2).MaxTotalSize(1000);

        var tooFew = await schema.SafeParseAsync(Collection(Make(length: 10)), cancellationToken: Token);
        Assert.Equal(ValidationErrorCode.TooSmall, Assert.Single(tooFew.Errors).Code);

        var tooBig = await schema.SafeParseAsync(Collection(Make(length: 900), Make(length: 900)), cancellationToken: Token);
        Assert.Equal(ValidationOrigin.Bytes, Assert.Single(tooBig.Errors).Info.Origin);
    }

    [Fact]
    public void One_Acceptable_Type_Is_Written_As_The_Content_It_Carries()
    {
        var document = Upload.File().ContentType("image/png").ToJsonSchema();

        Assert.Equal("string", document["type"]!.GetValue<string>());
        Assert.Equal("image/png", document["contentMediaType"]!.GetValue<string>());
    }

    [Fact]
    public void Several_Acceptable_Types_Are_Written_As_A_Union()
    {
        var document = Upload.File().ContentType("image/png", "image/jpeg").ToJsonSchema();

        var branches = document["anyOf"]!.AsArray();
        Assert.Equal(
            ["image/png", "image/jpeg"],
            branches.Select(branch => branch!["contentMediaType"]!.GetValue<string>()));
    }

    // A document saying "string" would describe text, and a file is not text. Saying "any bytes" is
    // incomplete; saying "text" would be wrong.
    [Fact]
    public void A_File_With_No_Declared_Type_Is_Written_As_Arbitrary_Bytes()
    {
        var document = Upload.File().MaxSize(10).ToJsonSchema();

        Assert.Equal("application/octet-stream", document["contentMediaType"]!.GetValue<string>());
    }

    // minLength and maxLength count the characters of a string. A multipart section has none, so a
    // size bound is left out rather than spelled with a keyword that means something else.
    [Fact]
    public void A_Size_Bound_Is_Reported_As_Something_The_Document_Cannot_Say()
    {
        var document = Upload.File().MaxSize(1000).ToJsonSchema();

        Assert.Null(document["maxLength"]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Upload.File().MaxSize(1000).ToJsonSchema(new JsonSchemaOptions
            {
                OnUnrepresentable = UnrepresentablePolicy.Throw,
            }));

        Assert.Contains("file size bound", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_Extension_And_A_Name_Rule_Are_Reported_As_Well()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Upload.File()
                .Extension(".png")
                .Named(Theo.String().MaxLength(8))
                .ToJsonSchema(new JsonSchemaOptions
                {
                    OnUnrepresentable = UnrepresentablePolicy.Throw,
                }));

        Assert.Contains("file extension rule", exception.Message, StringComparison.Ordinal);
        Assert.Contains("rule on the file name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Set_Of_Files_Is_Written_As_An_Array_Of_Them()
    {
        var document = Upload.Files()
            .MinCount(1)
            .MaxCount(5)
            .Each(Upload.File().ContentType("application/pdf"))
            .ToJsonSchema();

        Assert.Equal("array", document["type"]!.GetValue<string>());
        Assert.Equal(1, document["minItems"]!.GetValue<int>());
        Assert.Equal(5, document["maxItems"]!.GetValue<int>());
        Assert.Equal("application/pdf", document["items"]!["contentMediaType"]!.GetValue<string>());
    }

    [Fact]
    public void A_Total_Size_Budget_Is_Reported_As_Something_The_Document_Cannot_Say()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Upload.Files().MaxTotalSize(1000).ToJsonSchema(new JsonSchemaOptions
            {
                OnUnrepresentable = UnrepresentablePolicy.Throw,
            }));

        Assert.Contains("total size bound", exception.Message, StringComparison.Ordinal);
    }
}
