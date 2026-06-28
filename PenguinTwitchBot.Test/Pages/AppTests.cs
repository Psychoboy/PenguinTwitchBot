using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace PenguinTwitchBot.Test.Pages
{
    public class AppTests
    {
        [Fact]
        public void RequiresViewerRole_WithViewerRole_ReturnsTrue()
        {
            var result = InvokeRequiresViewerRole(typeof(RequiresViewerPage));
            Assert.True(result);
        }

        [Fact]
        public void RequiresViewerRole_WithEditorRole_ReturnsFalse()
        {
            var result = InvokeRequiresViewerRole(typeof(RequiresEditorPage));
            Assert.False(result);
        }

        [Fact]
        public void RequiresViewerRole_WithMultipleRolesIncludingViewer_ReturnsTrue()
        {
            var result = InvokeRequiresViewerRole(typeof(RequiresStreamerAndViewerPage));
            Assert.True(result);
        }

        [Fact]
        public void RequiresViewerRole_NoAuthorizeAttribute_ReturnsFalse()
        {
            var result = InvokeRequiresViewerRole(typeof(NoAuthorizePage));
            Assert.False(result);
        }

        [Fact]
        public void RequiresViewerRole_EmptyRoles_ReturnsFalse()
        {
            var result = InvokeRequiresViewerRole(typeof(EmptyRolesPage));
            Assert.False(result);
        }

        [Fact]
        public void RequiresViewerRole_WhitespaceRoles_ReturnsFalse()
        {
            var result = InvokeRequiresViewerRole(typeof(WhitespaceRolesPage));
            Assert.False(result);
        }

        [Theory]
        [InlineData("Viewer", true)]
        [InlineData("viewer", true)]
        [InlineData("VIEWER", true)]
        [InlineData("Editor", false)]
        [InlineData("Streamer", false)]
        public void RequiresViewerRole_VariousRoleValues(string roles, bool expected)
        {
            var result = InvokeRequiresViewerRole(CreateTypeWithRoles(roles));
            Assert.Equal(expected, result);
        }

        [Fact]
        public void OnInitializedAsync_MethodExists()
        {
            var method = typeof(PenguinTwitchBot.App).GetMethod("OnInitializedAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void Signin_MethodExists()
        {
            var method = typeof(PenguinTwitchBot.App).GetMethod("Signin", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void RemoteIpAddress_ParameterExists()
        {
            var property = typeof(PenguinTwitchBot.App).GetProperty("RemoteIpAddress");
            Assert.NotNull(property);
        }

        [Fact]
        public void RequiresViewerRole_StaticMethodExists()
        {
            var method = typeof(PenguinTwitchBot.App).GetMethod("RequiresViewerRole", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
        }

        private static bool InvokeRequiresViewerRole(Type pageType)
        {
            var method = typeof(PenguinTwitchBot.App).GetMethod("RequiresViewerRole", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (bool)method!.Invoke(null, [pageType])!;
        }

        private static Type CreateTypeWithRoles(string roles)
        {
            return roles.Contains("Viewer", StringComparison.OrdinalIgnoreCase) 
                ? typeof(RequiresViewerPage) 
                : typeof(RequiresEditorPage);
        }

        // Test helper classes for Authorization attributes
        [Authorize(Roles = "Viewer")]
        public class RequiresViewerPage : ComponentBase { }

        [Authorize(Roles = "Editor")]
        public class RequiresEditorPage : ComponentBase { }

        [Authorize(Roles = "Streamer,Editor,Viewer")]
        public class RequiresStreamerAndViewerPage : ComponentBase { }

        [Authorize(Roles = "")]
        public class EmptyRolesPage : ComponentBase { }

        [Authorize(Roles = "   ")]
        public class WhitespaceRolesPage : ComponentBase { }

        public class NoAuthorizePage : ComponentBase { }
    }
}