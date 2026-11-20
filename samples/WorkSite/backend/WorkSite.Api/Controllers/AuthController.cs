using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Problems;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkSite.Api.Auth;
using WorkSite.Api.Configuration;
using WorkSite.Api.Hubs;
using WorkSite.Api.Infrastructure;
using WorkSite.Api.Validation;
using WorkSite.Core.Contracts;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Controllers;

/// <summary>Registration, sign-in, token refresh, sign-out and account settings.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : WorkSiteControllerBase
{
    private const int MaxPersonNameLength = 100;
    private const string UsernameTaken = "That username is already taken.";
    private const string EmailTaken = "That email address is already registered.";

    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly SessionService _sessionService;
    private readonly CredentialChecker _credentials;
    private readonly IPasswordHasher<User> _hasher;
    private readonly AuthSettings _authSettings;
    private readonly SessionNotifier _sessionNotifier;

    /// <summary>Creates the controller.</summary>
    /// <param name="users">The user store.</param>
    /// <param name="sessions">The session store.</param>
    /// <param name="sessionService">Starts, refreshes and ends sessions.</param>
    /// <param name="credentials">Checks passwords.</param>
    /// <param name="hasher">Hashes new passwords.</param>
    /// <param name="settings">The password rules.</param>
    /// <param name="sessionNotifier">Tells the pages of an ended session to sign out.</param>
    public AuthController(IUserRepository users, ISessionRepository sessions, SessionService sessionService,
        CredentialChecker credentials, IPasswordHasher<User> hasher, WorkSiteSettings settings,
        SessionNotifier sessionNotifier)
    {
        _sessionNotifier = sessionNotifier;
        _users = users;
        _sessions = sessions;
        _sessionService = sessionService;
        _credentials = credentials;
        _hasher = hasher;
        _authSettings = settings.Auth;
    }

    /// <summary>Creates an account and signs it in.</summary>
    /// <param name="request">The new account's details.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>201 with the user; 400; 409 when the username or email address is taken.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        request ??= new RegisterRequest();
        var errors = new ValidationErrors();
        var firstName = InputRules.RequiredText(errors, "firstName", request.FirstName, "First name", MaxPersonNameLength);
        var lastName = InputRules.RequiredText(errors, "lastName", request.LastName, "Last name", MaxPersonNameLength);
        var email = InputRules.Email(errors, "email", request.Email, required: true);
        var username = InputRules.Username(errors, request.Username);
        InputRules.NewPassword(errors, "password", request.Password, _authSettings.MinimumPasswordLength,
            _authSettings.MaximumPasswordLength);
        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        var conflicts = new ValidationErrors();
        if (await _users.UsernameExistsAsync(username, cancellationToken: cancellationToken))
        {
            conflicts.Add("username", UsernameTaken);
        }

        if (await _users.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            conflicts.Add("email", EmailTaken);
        }

        if (conflicts.HasErrors)
        {
            return RegistrationConflict(conflicts);
        }

        User user;
        try
        {
            user = await _users.CreateAsync(new NewUser
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Username = username,
                PasswordHash = _hasher.HashPassword(null, request.Password),
            }, cancellationToken);
        }
        catch (DuplicateValueException e)
        {
            conflicts.Add(e.Field, e.Field == "email" ? EmailTaken : UsernameTaken);
            return RegistrationConflict(conflicts);
        }

        await _sessionService.StartAsync(Response, user.Id, cancellationToken);
        return Created("/api/auth/me", UserResponses.From(user));
    }

    /// <summary>Signs in with a username or email address and a password.</summary>
    /// <param name="request">The credentials.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the user; 400; 401 <c>invalid_credentials</c>.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        request ??= new LoginRequest();
        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            errors.Add("identifier", "Enter your username or email address.");
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors.Add("password", "Enter your password.");
        }

        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        var user = await _credentials.CheckAsync(request.Identifier, request.Password, cancellationToken);
        if (user == null)
        {
            return ProblemResult(WorkSiteProblems.InvalidCredentials());
        }

        await _sessionService.StartAsync(Response, user.Id, cancellationToken);
        return Ok(UserResponses.From(user));
    }

    /// <summary>Exchanges the refresh cookie for new cookies.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the user; empty 401 when there is no valid refresh token.</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [WireSessionRefresh]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var user = await _sessionService.RefreshAsync(HttpContext, cancellationToken);
        return user == null ? Unauthorized() : Ok(UserResponses.From(user));
    }

    /// <summary>Signs out: revokes the session and clears the cookies. Succeeds even when signed out.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>204.</returns>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var sessionId = User.GetSessionId();
        await _sessionService.EndAsync(HttpContext, cancellationToken);
        if (sessionId.HasValue)
        {
            await _sessionNotifier.SignedOutAsync(sessionId.Value);
        }

        return NoContent();
    }

    /// <summary>The signed-in user.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the user; empty 401 when not signed in.</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(CurrentUserId, cancellationToken);
        return user == null ? Unauthorized() : Ok(UserResponses.From(user));
    }

    /// <summary>Changes the signed-in user's name and email address.</summary>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the user; 400; 409 when the email address is taken.</returns>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileRequest request, CancellationToken cancellationToken)
    {
        request ??= new ProfileRequest();
        var errors = new ValidationErrors();
        var firstName = InputRules.RequiredText(errors, "firstName", request.FirstName, "First name", MaxPersonNameLength);
        var lastName = InputRules.RequiredText(errors, "lastName", request.LastName, "Last name", MaxPersonNameLength);
        var email = InputRules.Email(errors, "email", request.Email, required: true);
        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        var userId = CurrentUserId;
        if (await _users.EmailExistsAsync(email, userId, cancellationToken))
        {
            return ProblemResult(ApiProblems.Conflict(EmailTaken, "email", EmailTaken));
        }

        User user;
        try
        {
            user = await _users.UpdateProfileAsync(userId, firstName, lastName, email, cancellationToken);
        }
        catch (DuplicateValueException)
        {
            return ProblemResult(ApiProblems.Conflict(EmailTaken, "email", EmailTaken));
        }

        return user == null ? Unauthorized() : Ok(UserResponses.From(user));
    }

    /// <summary>Changes the signed-in user's password and signs out every other session.</summary>
    /// <param name="request">The current and new passwords.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>204; 400 when the current password is wrong or the new one is not allowed.</returns>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new ChangePasswordRequest();
        var errors = new ValidationErrors();
        if (string.IsNullOrEmpty(request.CurrentPassword))
        {
            errors.Add("currentPassword", "Enter your current password.");
        }

        InputRules.NewPassword(errors, "newPassword", request.NewPassword, _authSettings.MinimumPasswordLength,
            _authSettings.MaximumPasswordLength);
        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        var user = await _users.GetByIdAsync(CurrentUserId, cancellationToken);
        if (user == null)
        {
            return Unauthorized();
        }

        if (!await _credentials.CheckAsync(user, request.CurrentPassword, cancellationToken))
        {
            return Invalid("currentPassword", "The current password is incorrect.");
        }

        await _users.UpdatePasswordHashAsync(user.Id, _hasher.HashPassword(user, request.NewPassword), cancellationToken);
        var sessionId = User.GetSessionId();
        if (sessionId.HasValue)
        {
            await _sessions.RevokeOtherSessionsAsync(user.Id, sessionId.Value, cancellationToken);
            await _sessionNotifier.PasswordChangedAsync(user.Id, sessionId.Value);
        }

        return NoContent();
    }

    private static ObjectResult RegistrationConflict(ValidationErrors conflicts)
    {
        var errors = conflicts.ToDictionary();
        var title = errors.Count > 1
            ? "That username and email address are already in use."
            : errors.ContainsKey("email") ? EmailTaken : UsernameTaken;
        return ProblemResult(ApiProblems.Conflict(title, errors));
    }
}
