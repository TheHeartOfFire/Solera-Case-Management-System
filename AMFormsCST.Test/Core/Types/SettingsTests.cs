using AMFormsCST.Core.Types;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Types.UserSettings;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace AMFormsCST.Test.Core.Types;
public class SettingsTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var mockUserSettings = new Mock<IUserSettings>();
        var mockUiSettings = new Mock<IUiSettings>();

        // Act
        var settings = new Settings(mockUserSettings.Object, mockUiSettings.Object);

        // Assert
        Assert.Same(mockUserSettings.Object, settings.UserSettings);
        Assert.Same(mockUiSettings.Object, settings.UiSettings);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenUserSettingsIsNull()
    {
        // Arrange
        var mockUiSettings = new Mock<IUiSettings>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new Settings(null, mockUiSettings.Object));
        Assert.Contains("userSettings", ex.Message);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenUiSettingsIsNull()
    {
        // Arrange
        var mockUserSettings = new Mock<IUserSettings>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new Settings(mockUserSettings.Object, null));
        Assert.Contains("uiSettings", ex.Message);
    }

    [Fact]
    public void UserSettings_Property_CanBeSetAndGet()
    {
        // Arrange
        var mockUserSettings1 = new Mock<IUserSettings>();
        var mockUserSettings2 = new Mock<IUserSettings>();
        var mockUiSettings = new Mock<IUiSettings>();
        var settings = new Settings(mockUserSettings1.Object, mockUiSettings.Object);

        // Act
        settings.UserSettings = mockUserSettings2.Object;

        // Assert
        Assert.Same(mockUserSettings2.Object, settings.UserSettings);
    }

    [Fact]
    public void UiSettings_Property_CanBeGet()
    {
        // Arrange
        var mockUserSettings = new Mock<IUserSettings>();
        var mockUiSettings = new Mock<IUiSettings>();
        var settings = new Settings(mockUserSettings.Object, mockUiSettings.Object);

        // Act & Assert
        Assert.Same(mockUiSettings.Object, settings.UiSettings);
    }

    [Fact]
    public void AllSettings_ReturnsListWithUserAndUiSettings()
    {
        // Arrange
        var mockUserSettings = new Mock<IUserSettings>();
        var mockUiSettings = new Mock<IUiSettings>();
        var settings = new Settings(mockUserSettings.Object, mockUiSettings.Object);

        // Act
        var allSettings = settings.AllSettings;

        // Assert
        Assert.Equal(2, allSettings.Count);
        Assert.Contains(mockUserSettings.Object, allSettings);
        Assert.Contains(mockUiSettings.Object, allSettings);
    }

    [Fact]
    public void Settings_JsonRoundtrip_PersistsUserProfileAndSupportSettings()
    {
        // Arrange
        var orgVars = new AMFormsCST.Core.Types.UserSettings.AutomateFormsOrgVariables();
        var formsSettings = new AMFormsCST.Core.Types.UserSettings.AutomateFormsSettings
        {
            MailingName = "AM Forms Dept",
            StreetAddress = "123 Main St",
            City = "Albany",
            State = "NY",
            Zip = "12207"
        };
        var userSettings = new AMFormsCST.Core.Types.UserSettings.UserSettings(orgVars, formsSettings)
        {
            Name = "Jane Doe",
            Email = "jane.doe@solera.com",
            Phone = "555-123-4567",
            Title = "Senior Forms Specialist",
            Role = UserRole.AutomateForms,
            SupportEmail = "custom-support@solera.com",
            SupportPhone = "800-555-0199"
        };
        var uiSettings = new DummyUiSettings();
        var settings = new Settings(userSettings, uiSettings);

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Type == typeof(ISettings))
                        {
                            typeInfo.PolymorphismOptions = new System.Text.Json.Serialization.Metadata.JsonPolymorphismOptions
                            {
                                TypeDiscriminatorPropertyName = "$type",
                                DerivedTypes = { new(typeof(Settings), "settings") }
                            };
                        }
                        if (typeInfo.Type == typeof(IUserSettings))
                        {
                            typeInfo.PolymorphismOptions = new System.Text.Json.Serialization.Metadata.JsonPolymorphismOptions
                            {
                                TypeDiscriminatorPropertyName = "$type",
                                DerivedTypes = { new(typeof(AMFormsCST.Core.Types.UserSettings.UserSettings), "usersettings") }
                            };
                        }
                        if (typeInfo.Type == typeof(IAutomateFormsSettings))
                        {
                            typeInfo.PolymorphismOptions = new System.Text.Json.Serialization.Metadata.JsonPolymorphismOptions
                            {
                                TypeDiscriminatorPropertyName = "$type",
                                DerivedTypes = { new(typeof(AMFormsCST.Core.Types.UserSettings.AutomateFormsSettings), "automateformssettings") }
                            };
                        }
                        if (typeInfo.Type == typeof(IUiSettings))
                        {
                            typeInfo.PolymorphismOptions = new System.Text.Json.Serialization.Metadata.JsonPolymorphismOptions
                            {
                                TypeDiscriminatorPropertyName = "$type",
                                DerivedTypes = { new(typeof(DummyUiSettings), "dummyuisettings") }
                            };
                        }
                        if (typeInfo.Type == typeof(IOrgVariables))
                        {
                            typeInfo.PolymorphismOptions = new System.Text.Json.Serialization.Metadata.JsonPolymorphismOptions
                            {
                                TypeDiscriminatorPropertyName = "$type",
                                DerivedTypes = { new(typeof(AMFormsCST.Core.Types.UserSettings.AutomateFormsOrgVariables), "orgvars") }
                            };
                        }
                    }
                }
            }
        };

        // Act: Serialize and Deserialize
        var json = System.Text.Json.JsonSerializer.Serialize<ISettings>(settings, jsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ISettings>(json, jsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.UserSettings);
        Assert.Equal("Jane Doe", deserialized.UserSettings.Name);
        Assert.Equal("jane.doe@solera.com", deserialized.UserSettings.Email);
        Assert.Equal("555-123-4567", deserialized.UserSettings.Phone);
        Assert.Equal("Senior Forms Specialist", deserialized.UserSettings.Title);
        Assert.Equal(UserRole.AutomateForms, deserialized.UserSettings.Role);
        Assert.Equal("custom-support@solera.com", deserialized.UserSettings.SupportEmail);
        Assert.Equal("800-555-0199", deserialized.UserSettings.SupportPhone);
        Assert.NotNull(deserialized.UserSettings.Organization);
        Assert.IsType<AMFormsCST.Core.Types.UserSettings.AutomateFormsOrgVariables>(deserialized.UserSettings.Organization);
    }

    private class DummyUiSettings : IUiSettings
    {
        public List<ISetting> Settings { get; set; } = [];
    }
}