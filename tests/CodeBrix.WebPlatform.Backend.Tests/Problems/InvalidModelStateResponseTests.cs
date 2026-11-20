using System;
using System.Collections.Generic;
using CodeBrix.WebPlatform.Backend.Problems;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Problems;

public class InvalidModelStateResponseTests
{
    private static readonly string[] BodyParameter = { "payload" };

    private static ActionContext Context(params (string Key, string Message)[] errors)
    {
        var descriptor = new ActionDescriptor
        {
            Parameters = new List<ParameterDescriptor>
            {
                new() { Name = "payload", BindingInfo = new BindingInfo { BindingSource = BindingSource.Body } },
                new() { Name = "count", BindingInfo = new BindingInfo { BindingSource = BindingSource.Query } },
            },
        };
        var context = new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);
        foreach (var (key, message) in errors)
        {
            context.ModelState.AddModelError(key, message);
        }

        return context;
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData("$", "body")]
    [InlineData("payload", "body")]
    [InlineData("Payload", "body")]
    [InlineData("$.hours", "hours")]
    [InlineData("$.Hours", "hours")]
    [InlineData("$.lines[2].amount", "lines")]
    [InlineData("$.address.city", "address")]
    [InlineData("Count", "count")]
    [InlineData("count", "count")]
    [InlineData("[0]", "body")]
    [InlineData("request", "request")]
    public void FieldName_maps_a_model_state_key_to_a_camel_case_field(string key, string expected)
        => InvalidModelStateResponse.FieldName(key, BodyParameter).Should().Be(expected);

    [Fact]
    public void Create_answers_400_validation_failed_with_one_message_per_field()
    {
        //Arrange
        var context = Context(("$.hours", "x"), ("$.hours", "y"), ("payload", "z"), ("Count", "w"));

        //Act
        var result = (ApiProblemResult)InvalidModelStateResponse.Create(context);

        //Assert
        result.StatusCode.Should().Be(400);
        result.ContentTypes.Should().Equal(ApiProblem.ContentType);
        result.Problem.Code.Should().Be("validation_failed");
        result.Problem.Errors.Keys.Should().BeEquivalentTo("hours", "body", "count");
        result.Problem.Errors["hours"].Should().Equal(InvalidModelStateResponse.ValueMessage);
        result.Problem.Errors["body"].Should().Equal(InvalidModelStateResponse.BodyMessage);
    }

    [Fact]
    public void Create_without_any_error_still_names_the_body()
    {
        //Act
        var result = (ApiProblemResult)InvalidModelStateResponse.Create(Context());

        //Assert
        result.Problem.Errors.Keys.Should().Equal("body");
    }

    [Fact]
    public void Create_refuses_a_null_context()
    {
        //Act
        Action act = () => InvalidModelStateResponse.Create(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
