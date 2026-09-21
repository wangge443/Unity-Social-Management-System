namespace SocialSystem.Api.DTOs;

public sealed record DatabaseCheckResponse(string Status, string Database, int TablesChecked);

