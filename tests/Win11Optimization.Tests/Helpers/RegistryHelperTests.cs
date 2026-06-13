using Microsoft.Win32;
using Win11Optimization.Services.Helpers;
using Xunit;

namespace Win11Optimization.Tests.Helpers;

/// <summary>
/// Тесты для утилит работы с реестром (RegistryHelper).
/// Использует безопасную песочницу в HKCU\Software\Win11OptimizationTests.
/// </summary>
public class RegistryHelperTests : IDisposable
{
    private const string TestSubKey = @"Software\Win11OptimizationTests";

    public RegistryHelperTests()
    {
        // Подготовка: очистка песочницы перед тестом
        CleanupSandbox();
    }

    public void Dispose()
    {
        // Очистка: удаление песочницы после теста
        CleanupSandbox();
    }

    private void CleanupSandbox()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(TestSubKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // Игнорируем ошибки при очистке
        }
    }

    [Fact]
    public void SetDword_ShouldCreateKeyAndValue()
    {
        // Arrange
        var valueName = "TestValue";
        var expectedValue = 42;

        // Act
        RegistryHelper.SetDword(Registry.CurrentUser, TestSubKey, valueName, expectedValue);

        // Assert
        using var key = Registry.CurrentUser.OpenSubKey(TestSubKey);
        Assert.NotNull(key);
        
        var actualValue = key.GetValue(valueName);
        Assert.NotNull(actualValue);
        Assert.Equal(expectedValue, actualValue);
        Assert.Equal(RegistryValueKind.DWord, key.GetValueKind(valueName));
    }

    [Fact]
    public void GetDword_ShouldReturnCorrectValue()
    {
        // Arrange
        var valueName = "TestReadValue";
        var expectedValue = 100;
        
        using (var key = Registry.CurrentUser.CreateSubKey(TestSubKey))
        {
            key.SetValue(valueName, expectedValue, RegistryValueKind.DWord);
        }

        // Act
        var result = RegistryHelper.GetDword(Registry.CurrentUser, TestSubKey, valueName);

        // Assert
        Assert.True(result.HasValue);
        Assert.Equal(expectedValue, result.Value);
    }

    [Fact]
    public void GetDword_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Act
        var result = RegistryHelper.GetDword(Registry.CurrentUser, @"Software\NonExistentTestKey123", "Value");

        // Assert
        Assert.False(result.HasValue);
    }

    [Fact]
    public void GetDword_ShouldReturnNull_WhenValueDoesNotExist()
    {
        // Arrange
        Registry.CurrentUser.CreateSubKey(TestSubKey).Dispose();

        // Act
        var result = RegistryHelper.GetDword(Registry.CurrentUser, TestSubKey, "NonExistentValue");

        // Assert
        Assert.False(result.HasValue);
    }

    [Fact]
    public void DeleteValue_ShouldRemoveValue()
    {
        // Arrange
        var valueName = "ValueToDelete";
        using (var key = Registry.CurrentUser.CreateSubKey(TestSubKey))
        {
            key.SetValue(valueName, 1, RegistryValueKind.DWord);
        }

        // Убеждаемся, что значение создано
        Assert.NotNull(Registry.CurrentUser.OpenSubKey(TestSubKey)?.GetValue(valueName));

        // Act
        RegistryHelper.DeleteValue(Registry.CurrentUser, TestSubKey, valueName);

        // Assert
        Assert.Null(Registry.CurrentUser.OpenSubKey(TestSubKey)?.GetValue(valueName));
    }

    [Fact]
    public void DeleteValue_ShouldNotThrow_WhenValueDoesNotExist()
    {
        // Arrange
        Registry.CurrentUser.CreateSubKey(TestSubKey).Dispose();

        // Act & Assert (Should not throw)
        var exception = Record.Exception(() => 
            RegistryHelper.DeleteValue(Registry.CurrentUser, TestSubKey, "NonExistentValue"));
        
        Assert.Null(exception);
    }
}
