﻿using FastEndpoints;

using Vapour.Shared.Configuration.Profiles.Schema;
using Vapour.Shared.Configuration.Profiles.Services;

namespace Vapour.Server.Host.Profile.Endpoints;

public sealed class ProfileSaveEndpoint : Endpoint<DS4WindowsProfile, DS4WindowsProfile>
{
    private readonly IProfilesService _profilesService;

    public ProfileSaveEndpoint(IProfilesService profilesService)
    {
        _profilesService = profilesService;
    }

    public override void Configure()
    {
        Verbs(Http.PUT);
        Routes("/profile/save");
        AllowAnonymous();
        Summary(s => {
            s.Summary = "Saves a given profile";
            s.Description = "Saves/overwrites a profile or creates a new one, if the ID wasn't known";
            s.Responses[200] = "The profile has been saved successfully";
        });
    }

    public override async Task HandleAsync(DS4WindowsProfile req, CancellationToken ct)
    {
        _profilesService.CreateOrUpdateProfile(req);

        await SendOkAsync(req, ct);
    }
}