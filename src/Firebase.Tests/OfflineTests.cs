namespace Firebase.Database.Tests
{
    using Firebase.Database;
    using Firebase.Database.Tests.Entities;
    using Firebase.Database.Offline;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Reactive.Testing;
    using System;
    using System.Linq;
    using System.Reactive;
    using System.Reactive.Concurrency;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Threading.Tasks;
    using WorldDomination.Net.Http;
    using Firebase.Database.Query;

    [TestClass]
    public class OfflineTests
    {
        public const string BasePath = "http://base.path.net";

        [TestMethod]
        public void Test()
        {
            FirebaseOptions options = new FirebaseOptions()
            {
                OfflineDatabaseFactory = (t, s) =>
                {
                    return new OfflineDatabase(t, s);
                }
            };
            
            var client = new FirebaseClient(BasePath, options);

            var dinosaurDB = client.Child("dinos").AsRealtimeDatabase<Dinosaur>(
                "",
                "",
                StreamingOptions.None,
                InitialPullStrategy.None,
                true);

            dinosaurDB.Database.Clear();
            Dinosaur dino = new Dinosaur(5, 20, 30);
            dinosaurDB.Post(dino);

            //var dinosaurDB2 = client.Child("dinos").AsRealtimeDatabase<Dinosaur>(
            //    "",
            //    "",
            //    StreamingOptions.LatestOnly,
            //    InitialPullStrategy.MissingOnly,
            //    true);

            //Assert.AreEqual(dinosaurDB2.Database.Count, 1);

            TestScheduler scheduler = new TestScheduler();

            var dino1 = new Dinosaur(1, 1, 1);
            scheduler.Schedule(TimeSpan.FromTicks(10), () =>
            {
                dinosaurDB.Post(dino1);
            });

            //ITestableObserver<Dinosaur> results = scheduler.Start(
            //    () => dinosaurDB.AsObservable().Select(x => x.Object),
            //    created: 0,
            //    subscribed: 5,
            //    disposed: 100);
            ITestableObserver<Dinosaur> results = scheduler.CreateObserver<Dinosaur>();
            dinosaurDB.AsObservable().Select(x => x.Object).Subscribe(results);
            scheduler.Start();
            results.Messages.AssertEqual(
                ReactiveTest.OnNext(0, dino),
                ReactiveTest.OnNext(10, dino1));
            
            //var dino2 = new Dinosaur(2, 2, 2);
            //var dino3 = new Dinosaur(3, 3, 3);

            //ITestableObservable<Dinosaur> source = scheduler.CreateHotObservable(
            //    ReactiveTest.OnNext(10, dino1),
            //    ReactiveTest.OnNext(20, dino2),
            //    ReactiveTest.OnNext(30, dino3));

            //ITestableObserver<Dinosaur> results = scheduler.CreateObserver<Dinosaur>();
            //source.Subscribe(results);
            //scheduler.Start();
            //results.Messages.AssertEqual(
            //    ReactiveTest.OnNext(10, dino1),
            //    ReactiveTest.OnNext(20, dino2),
            //    ReactiveTest.OnNext(30, dino3));

            //Assert.AreEqual(dinosaurDB.Database.Count, 3);

            dinosaurDB.Database.Clear();
        }

        [TestMethod]
        public async Task PushDataAsync()
        {
            const string responseData = @"{
              ""dino1"": {
                ""ds"": {
                  ""height"" : 2,
                  ""length"" : 2,
                  ""weight"": 2
                }
              }";

            //const string responseData = "{ \"Id\":69, \"Name\":\"Jane\" }";
            var messageResponse = FakeHttpMessageHandler.GetStringHttpResponseMessage(responseData);

            // Prepare our 'options' with all of the above fake stuff.
            var httpMsgOptions = new HttpMessageOptions
            {
                //RequestUri = new Uri(BasePath + "/dinos/dino1"),
                HttpResponseMessage = messageResponse
            };


            FirebaseOptions firebaseOptions = new FirebaseOptions()
            {
                OfflineDatabaseFactory = (t, s) =>
                {
                    return new OfflineDatabase(t, s);
                }
            };

            var httpMessageHandler = new FakeHttpMessageHandler(httpMsgOptions);
            var client = new FirebaseClient(BasePath, firebaseOptions, httpMessageHandler);

            var dinosaurDB = client.Child("dinos").AsRealtimeDatabase<Dinosaur>(
                "",
                "",
                StreamingOptions.Everything,
                InitialPullStrategy.None,
                true);

            TestScheduler scheduler = new TestScheduler();
            ITestableObserver<Dinosaur> results = scheduler.CreateObserver<Dinosaur>();
            dinosaurDB.AsObservable().Select(x => x.Object).Subscribe(results);

            await client.Child("dinos/dino1").PutAsync(responseData);

            results.Messages.Count.ShouldBeEquivalentTo(1);
        }
    }
}
