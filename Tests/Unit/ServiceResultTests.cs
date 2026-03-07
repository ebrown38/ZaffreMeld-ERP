using FluentAssertions;
using Xunit;
using ZaffreMeld.Web.Services;

namespace ZaffreMeld.Tests.Unit;

/// <summary>
/// Tests for the ServiceResult record — the core return type that replaced
/// the original Java String[] { "0"/"1", "message" } pattern.
/// </summary>
public class ServiceResultTests
{
    [Fact]
    public void Ok_WithNoArgs_HasSuccessTrueAndDefaultMessage()
    {
        var result = ServiceResult.Ok();

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Record saved successfully.");
        result.Data.Should().BeNull();
    }

    [Fact]
    public void Ok_WithCustomMessage_PreservesMessage()
    {
        var result = ServiceResult.Ok("Item added successfully.");

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Item added successfully.");
    }

    [Fact]
    public void Ok_WithData_ExposesData()
    {
        var data = new { Id = "SO-001" };
        var result = ServiceResult.Ok("Created.", data);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(data);
    }

    [Fact]
    public void Error_HasSuccessFalse()
    {
        var result = ServiceResult.Error("Account not found.");

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Account not found.");
        result.Data.Should().BeNull();
    }

    [Fact]
    public void Error_WithEmptyMessage_StillSuccessFalse()
    {
        var result = ServiceResult.Error(string.Empty);

        result.Success.Should().BeFalse();
        result.Message.Should().BeEmpty();
    }

    [Fact]
    public void Ok_And_Error_AreDistinct()
    {
        ServiceResult.Ok().Success.Should().NotBe(ServiceResult.Error("x").Success);
    }

    [Fact]
    public void Record_Equality_WorksOnValues()
    {
        var a = ServiceResult.Ok("saved.");
        var b = ServiceResult.Ok("saved.");
        a.Should().Be(b);
    }
}
