namespace Firebase.Database.Tests
{
    using Firebase.Database;
    using Firebase.Database.Offline;
    using Firebase.Database.Streaming;
    using Firebase.Database.Tests.Entities;
    using Firebase.Database.Tests.Utils;
    using FluentAssertions;
    using Microsoft.Reactive.Testing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Concurrency;
    using System.Reactive.Linq;

    [TestClass]
    public class OfflineTests
    {
        public const string BasePath = "http://base.path.net";

        const string ResponseData_OneDino = @"{
              ""dino1"": {
                ""ds"": {
                  ""height"" : 1,
                  ""length"" : 1,
                  ""weight"": 1
                }
              }
            }";

        const string ResponseData_TwoDinos = @"{
              ""dino2"": {
                ""ds"": {
                  ""height"" : 2,
                  ""length"" : 2,
                  ""weight"": 2
                }
              },
              ""dino3"": {
                ""ds"": {
                  ""height"" : 3,
                  ""length"" : 3,
                  ""weight"": 3
                }
              }
            }";

        [TestMethod]
        public void PostDinosaurAndMakeSureItsLoadedWhenCreatingANewDbRef()
        {
            var dinosaurDB = GetOfflineDinosaurDB();
            RealtimeDatabase<Dinosaur> dinosaurDB2 = null;

            try
            {
                Dinosaur dino = new Dinosaur(5, 20, 30);
                dinosaurDB.Post(dino);

                dinosaurDB2 = GetOfflineDinosaurDB();

                Assert.AreEqual(1, dinosaurDB.Database.Count);
                Assert.AreEqual(1, dinosaurDB2.Database.Count);
            }
            finally
            {
                CleanUp(dinosaurDB, dinosaurDB2);
            }
        }

        [TestMethod]
        public void PutDinosaurAndMakeSureItsIdenticalToTheOneInTheDatabase()
        {
            var dinosaurDB = GetOfflineDinosaurDB();

            try
            {
                Dinosaur dino = new Dinosaur(5, 20, 30);
                dinosaurDB.Put("dino1", dino);

                var recoveredDino = dinosaurDB.Database["dino1"].Deserialize<Dinosaur>();

                Assert.AreEqual(dino.Dimensions.Height, recoveredDino?.Dimensions.Height);
                Assert.AreEqual(dino.Dimensions.Length, recoveredDino?.Dimensions.Length);
                Assert.AreEqual(dino.Dimensions.Weight, recoveredDino?.Dimensions.Weight);
            }
            finally
            {
                CleanUp(dinosaurDB);
            }
        }

        [TestMethod]
        public void PostThreeDinosaursAndRetrieveThemAll()
        {
            var dinosaurDB = GetOfflineDinosaurDB();

            try
            {
                Dinosaur dino1 = new Dinosaur(1, 1, 1);
                dinosaurDB.Post(dino1);
                Dinosaur dino2 = new Dinosaur(2, 2, 2);
                dinosaurDB.Post(dino2);
                Dinosaur dino3 = new Dinosaur(3, 3, 3);
                dinosaurDB.Post(dino3);

                IEnumerable<FirebaseObject<Dinosaur>> offlineDinos = dinosaurDB.Once();

                Assert.AreEqual(3, offlineDinos.Count());
            }
            finally
            {
                CleanUp(dinosaurDB);
            }
        }

        // TODO: Figure out why this test passes when run alone, but fails when running as a group.
        [TestMethod]
        public void ObserveDbAndDoInitialPullOfEverything()
        {
            var dinosaurDB = GetOfflineDinosaurDB(ResponseData_TwoDinos, StreamingOptions.None, InitialPullStrategy.Everything);

            try
            {
                var stream = dinosaurDB
                    .AsObservable()
                    .Where(x => x.Object != null);

                TestScheduler scheduler = new TestScheduler();
                ITestableObserver<FirebaseEvent<Dinosaur>> results = scheduler.Start(
                    () => stream,
                    created: 0,
                    subscribed: 0,
                    disposed: 10);

                Assert.AreEqual(2, results.Messages.Count);
                Assert.AreEqual(2, dinosaurDB.Database.Count());

                var firebaseEvent1 = results.Messages[0].Value.Value;
                firebaseEvent1.Key.ShouldBeEquivalentTo("dino2");
                firebaseEvent1.EventSource.ShouldBeEquivalentTo(FirebaseEventSource.OnlineInitial);
                firebaseEvent1.EventType.ShouldBeEquivalentTo(FirebaseEventType.InsertOrUpdate);
            }
            finally
            {
                CleanUp(dinosaurDB);
            }
        }

        [TestMethod]
        public void ObserveDbAndMakeSureNoEventsAreReceived()
        {
            var dinosaurDB = GetOfflineDinosaurDB(ResponseData_TwoDinos, StreamingOptions.None, InitialPullStrategy.None);

            try
            {
                var stream = dinosaurDB.AsObservable()
                    .Where(x => x.Object != null);

                TestScheduler scheduler = new TestScheduler();
                ITestableObserver<FirebaseEvent<Dinosaur>> results = scheduler.CreateObserver<FirebaseEvent<Dinosaur>>();
                stream.Subscribe(results);
                scheduler.Start();

                Assert.AreEqual(0, results.Messages.Count);
                Assert.AreEqual(0, dinosaurDB.Database.Count());
            }
            finally
            {
                CleanUp(dinosaurDB);
            }
        }

        [TestMethod]
        public void ObserveDbAndMakeSureTwoPostedDinosaursAreReceived()
        {
            var dinosaurDB = GetOfflineDinosaurDB(ResponseData_TwoDinos, StreamingOptions.None, InitialPullStrategy.None);

            try
            {
                var stream = dinosaurDB.AsObservable()
                    .Where(x => x.Object != null);

                TestScheduler scheduler = new TestScheduler();
                scheduler.Schedule(TimeSpan.FromTicks(1), () =>
                {
                    dinosaurDB.Post(new Dinosaur(4, 4, 4));
                });
                scheduler.Schedule(TimeSpan.FromTicks(5), () =>
                {
                    dinosaurDB.Post(new Dinosaur(5, 5, 5));
                });

                ITestableObserver<FirebaseEvent<Dinosaur>> results = scheduler.CreateObserver<FirebaseEvent<Dinosaur>>();
                stream.Subscribe(results);
                scheduler.Start();

                Assert.AreEqual(2, results.Messages.Count);
                Assert.AreEqual(2, dinosaurDB.Database.Count());

                var firebaseEvent1 = results.Messages[0].Value.Value;
                firebaseEvent1.EventSource.ShouldBeEquivalentTo(FirebaseEventSource.Offline);
                firebaseEvent1.EventType.ShouldBeEquivalentTo(FirebaseEventType.InsertOrUpdate);
                results.Messages[1].Time.ShouldBeEquivalentTo(5);
            }
            finally
            {
                CleanUp(dinosaurDB);
            }
        }

        private RealtimeDatabase<Dinosaur> GetOfflineDinosaurDB(
            string responseData = "",
            StreamingOptions streamingOptions = StreamingOptions.LatestOnly,
            InitialPullStrategy initialPullStrategy = InitialPullStrategy.MissingOnly)
        {
            var messageResponse = FakeHttpMessageHandler.GetStringHttpResponseMessage(responseData);
            var httpMsgOptions = new HttpMessageOptions
            {
                HttpResponseMessage = messageResponse
            };

            FirebaseOptions firebaseOptions = new FirebaseOptions()
            {
                OfflineDatabaseFactory = (t, s) => new OfflineDatabase(t, s),
                HttpMessageHandler = new FakeHttpMessageHandler(httpMsgOptions)
            };

            var client = new FirebaseClient(BasePath, firebaseOptions);

            var dinosaurDB = client.Child("dinos").AsRealtimeDatabase<Dinosaur>(
                string.Empty,
                string.Empty,
                streamingOptions,
                initialPullStrategy,
                true);

            return dinosaurDB;
        }

        private void CleanUp<T>(params RealtimeDatabase<T>[] databases)
            where T : class
        {
            foreach(var db in databases)
            {
                (db?.Database as OfflineDatabase)?.ReleaseFile();
            }

            foreach(var db in databases)
            {
                (db?.Database as OfflineDatabase)?.DeleteFile();
            }
        }
    }
}
