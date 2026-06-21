using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FluentAssertions;
using OrderGenerator.Application.Services;
using Xunit;

namespace OrderGenerator.UnitTests.Services
{
    public class OrderTrackerTests
    {
        [Fact]
        public void GenerateClientOrderID_ShouldGenerateIDsMatchingPattern()
        {
            // Arrange
            var tracker = new OrderTracker();
            string expectedDatePrefix = $"CLT-{DateTime.UtcNow:yyyyMMdd}-";

            // Act
            string id1 = tracker.GenerateClientOrderID();
            string id2 = tracker.GenerateClientOrderID();

            // Assert
            id1.Should().StartWith(expectedDatePrefix).And.EndWith("000001");
            id2.Should().StartWith(expectedDatePrefix).And.EndWith("000002");
            id1.Should().NotBe(id2);
        }

        [Fact]
        public void GenerateClientOrderID_ShouldBeThreadSafeAndGenerateUniqueIDs()
        {
            // Arrange
            var tracker = new OrderTracker();
            var ids = new ConcurrentDictionary<string, byte>();
            int taskCount = 100;

            // Act
            Parallel.For(0, taskCount, _ =>
            {
                string id = tracker.GenerateClientOrderID();
                ids.TryAdd(id, 0);
            });

            // Assert
            ids.Count.Should().Be(taskCount);
        }
    }
}
