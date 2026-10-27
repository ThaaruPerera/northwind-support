using System.Globalization;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Extensions;

/// <summary>Who is signed in, read from the validated token on the current request.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int? AgentId
    {
        get
        {
            var subject = accessor.HttpContext?.User.FindFirst("sub")?.Value;

            return int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }
}
