using Xunit;

namespace RbxDisplay;

public sealed class GameIconTests
{
    [Fact(DisplayName = "completed thumbnail returns its image url")]
    public void CompletedThumbnailReturnsItsImageUrl()
    {
        string body = """{"data":[{"targetId":5,"state":"Completed","imageUrl":"https://tr.rbxcdn.com/icon.png"}]}""";
        Assert.Equal("https://tr.rbxcdn.com/icon.png", GameIcon.ReadImageUrl(body, 5));
    }

    [Fact(DisplayName = "pending thumbnail has no image")]
    public void PendingThumbnailHasNoImage()
    {
        string body = """{"data":[{"targetId":5,"state":"Pending","imageUrl":""}]}""";
        Assert.Null(GameIcon.ReadImageUrl(body, 5));
    }

    [Fact(DisplayName = "a different universe in the payload is ignored")]
    public void DifferentUniverseIsIgnored()
    {
        string body = """{"data":[{"targetId":9,"state":"Completed","imageUrl":"https://tr.rbxcdn.com/other.png"},{"targetId":5,"state":"Completed","imageUrl":"https://tr.rbxcdn.com/icon.png"}]}""";
        Assert.Equal("https://tr.rbxcdn.com/icon.png", GameIcon.ReadImageUrl(body, 5));
    }
}
