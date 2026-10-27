namespace SupportDesk.Application.Exceptions;

/// <summary>
/// The caller could not be authenticated. Always reported as <c>401</c>, and always with a message
/// that says nothing about which part was wrong.
/// </summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);
