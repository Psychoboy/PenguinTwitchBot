using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace PenguinTwitchBot.Helpers;

public static class AuthenticationNavigationHelper
{
    public static void NavigateToSigninWithReturnUrl(this NavigationManager navigation, bool forceLoad = true)
    {
        var currentUri = new Uri(navigation.Uri);
        var returnPath = string.IsNullOrEmpty(currentUri.PathAndQuery) ? "/" : currentUri.PathAndQuery;
        if (!string.IsNullOrEmpty(currentUri.Fragment))
        {
            returnPath += currentUri.Fragment;
        }

        var signinUrl = QueryHelpers.AddQueryString("/signin", "r", returnPath);
        navigation.NavigateTo(signinUrl, forceLoad);
    }
}
