namespace Entry.Dto;

/// <summary>Error body: <c>{ "message": "..." }</c>.</summary>
public class MessageResponseDto
{
    public MessageResponseDto() { }

    public MessageResponseDto(string message) => Message = message;

    public string Message { get; set; } = null!;
}
