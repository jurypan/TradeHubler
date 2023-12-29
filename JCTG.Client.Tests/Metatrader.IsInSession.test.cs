namespace JCTG.Client.Tests
{
    public class MetatraderIsInSessionTests
    {
        private Metatrader mt;

        [SetUp]
        public void Setup()
        {
            mt = new Metatrader(new AppConfig());
        }

        [Test]
        public void Test_SessionStartsOnSundayEndsOnMonday2()
        {
            // Arrange
            var sessions = new Dictionary<string, SessionTimes>
            {
                ["Sunday"] = new SessionTimes { Open = new TimeSpan(23, 00, 0), Close = null },
                ["Monday"] = new SessionTimes { Open = new TimeSpan(23, 00, 0), Close = new TimeSpan(6, 0, 0) },
                ["Tuesday"] = new SessionTimes { Open = null, Close = new TimeSpan(6, 0, 0) }
            };

            // Act
            var result = mt.IsCurrentUTCTimeInSession(sessions, new DateTime(2023, 12, 19, 0, 0, 0));

            // Assert
            Assert.That(result);
        }

        [Test]
        public void Test_SessionStartsOnSundayEndsOnMonday()
        {
            // Arrange
            var sessions = new Dictionary<string, SessionTimes>
            {
                ["Sunday"] = new SessionTimes { Open = new TimeSpan(15, 20, 0), Close = null },
                ["Monday"] = new SessionTimes { Open = null, Close = new TimeSpan(3, 0, 0) }
            };

            // Act
            var result = mt.IsCurrentUTCTimeInSession(sessions, new DateTime(2023, 12, 17, 20, 0, 0));

            // Assert
            Assert.That(result);
        }

        [Test]
        public void Test_SessionStartsOnFridayEndsOnSaturday()
        {
            // Arrange
            var sessions = new Dictionary<string, SessionTimes>
            {
                ["Friday"] = new SessionTimes { Open = new TimeSpan(15, 20, 0), Close = null },
                ["Saturday"] = new SessionTimes { Open = null, Close = new TimeSpan(3, 0, 0) }
            };

            // Act
            var result = mt.IsCurrentUTCTimeInSession(sessions, new DateTime(2023, 12, 15, 20, 0, 0));
            
            // Assert
            Assert.That(result);
        }

        [Test]
        public void Test_SessionClosedForCurrentDay()
        {
            var sessions = new Dictionary<string, SessionTimes>
            {
                ["Sunday"] = null // Closed on Sunday
                                  // ... other days
            };

            var result = mt.IsCurrentUTCTimeInSession(sessions, new DateTime(2023, 12, 17, 10, 0, 0));

            Assert.That(!result);
        }

        // 2. Test a normal weekday session where the current time is within the session (e.g., Monday afternoon)
        [Test]
        public void Test_WithinNormalWeekdaySession()
        {
            var sessions = new Dictionary<string, SessionTimes>
            {
                ["Monday"] = new SessionTimes { Open = new TimeSpan(9, 0, 0), Close = new TimeSpan(17, 0, 0) }
                // ... other days
            };

            var result = mt.IsCurrentUTCTimeInSession(sessions, new DateTime(2023, 12, 18, 15, 0, 0));

            Assert.That(result);
        }

        // 3. Test a normal weekday session where the current time is outside the session (e.g., Monday morning)
        [Test]
        public void Test_OutsideNormalWeekdaySession()
        {
            var sessions = new Dictionary<string, SessionTimes>
            {
                ["Monday"] = new SessionTimes { Open = new TimeSpan(9, 0, 0), Close = new TimeSpan(17, 0, 0) }
                // ... other days
            };
            var result = mt.IsCurrentUTCTimeInSession(sessions, new DateTime(2023, 12, 18, 8, 0, 0));

            Assert.That(!result);
        }

        // 4. Test a session that goes from one day to another, where the current time is after midnight but still within the session (e.g., Saturday to Sunday)
        [Test]
        public void Test_SessionOverMidnight()
        {
            var sessions = new Dictionary<string, SessionTimes>
            {
                ["Saturday"] = new SessionTimes { Open = new TimeSpan(22, 0, 0), Close = null },
                ["Sunday"] = new SessionTimes { Open = null, Close = new TimeSpan(2, 0, 0) }
                // ... other days
            };

            var result = mt.IsCurrentUTCTimeInSession(sessions, new DateTime(2023, 12, 17, 1, 0, 0));

            Assert.That(result);
        }

        // 5. Test when the `sessions` dictionary is empty (no sessions defined)
        [Test]
        public void Test_EmptySessionsDictionary()
        {
            var sessions = new Dictionary<string, SessionTimes>();

            var result = mt.IsCurrentUTCTimeInSession(sessions, new DateTime(2023, 12, 18, 10, 0, 0));

            Assert.That(!result);
        }
    }
}