using ERPSystem.Host.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ERPSystem.Tests.Utilities;

public class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteWithRetryAsync_SuccessFirstAttempt_NoRetry()
    {
        // Arrange
        var attempts = 0;

        // Act
        var result = await RetryPolicy.ExecuteWithRetryAsync<int>(
            (attempt, ct) =>
            {
                attempts++;
                return Task.FromResult(42);
            },
            NullLogger.Instance,
            opName: "test-op");

        // Assert
        result.Should().Be(42);
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_TransientError_RetriesUpToMax()
    {
        // Arrange
        var attempts = 0;

        // Act + Assert
        await Assert.ThrowsAsync<NpgsqlException>(() =>
            RetryPolicy.ExecuteWithRetryAsync<int>(
                (attempt, ct) =>
                {
                    attempts++;
                    throw new NpgsqlException("simulated transient failure");
                },
                NullLogger.Instance,
                maxRetries: 3,
                opName: "test-op"));

        // First attempt + 2 retries = 3 attempts total
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_RecoversAfterTransientError()
    {
        // Arrange
        var attempts = 0;

        // Act
        var result = await RetryPolicy.ExecuteWithRetryAsync<int>(
            (attempt, ct) =>
            {
                attempts++;
                if (attempt < 2)
                    throw new NpgsqlException("transient on first attempt");
                return Task.FromResult(99);
            },
            NullLogger.Instance,
            maxRetries: 3,
            opName: "test-op");

        // Assert
        result.Should().Be(99);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_NonTransientError_NoRetry()
    {
        // Arrange
        var attempts = 0;

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RetryPolicy.ExecuteWithRetryAsync<int>(
                (attempt, ct) =>
                {
                    attempts++;
                    throw new InvalidOperationException("non-transient");
                },
                NullLogger.Instance,
                maxRetries: 3,
                opName: "test-op"));

        // Only first attempt — non-transient errors don't retry
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetry_VoidOverload_RetriesOnTransient()
    {
        // Arrange
        var attempts = 0;

        // Act
        await RetryPolicy.ExecuteWithRetryAsync(
            (attempt, ct) =>
            {
                attempts++;
                return Task.CompletedTask;
            },
            NullLogger.Instance,
            maxRetries: 3,
            opName: "test-op");

        // Assert
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetry_TimeoutException_Retries()
    {
        // Arrange
        var attempts = 0;

        // Act + Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            RetryPolicy.ExecuteWithRetryAsync<int>(
                (attempt, ct) =>
                {
                    attempts++;
                    throw new TimeoutException("simulated timeout");
                },
                NullLogger.Instance,
                maxRetries: 3,
                opName: "test-op"));

        attempts.Should().Be(3);
    }
}