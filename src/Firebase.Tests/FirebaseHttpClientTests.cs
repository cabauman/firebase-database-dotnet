namespace Firebase.Database.Tests
{
    using Firebase.Database;
    using Firebase.Database.Query;
    using Firebase.Database.Streaming;
    using Firebase.Database.Tests.Entities;
    using Firebase.Database.Tests.Utils;
    using FluentAssertions;
    using Microsoft.Reactive.Testing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Reactive.Linq;
    using System.Reactive.Threading.Tasks;
    using System.Threading.Tasks;

    [TestClass]
    public class FirebaseHttpClientTests
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

        const string ResponseData_Dino3Height = @"{
              ""dino3"": {
                ""ds"": {
                  ""height"" : 1
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
        public void PostAsyncShouldNotThrowAnException()
        {
            var client = GetFirebaseClient(ResponseData_OneDino);

            Func<Task> post = async () => { await client.Child("dinos").PostAsync(ResponseData_OneDino); };
            post.ShouldNotThrow<FirebaseException>();
        }

        [TestMethod]
        public void PutAsyncShouldNotThrowAnException()
        {
            var client = GetFirebaseClient(ResponseData_OneDino);

            Func<Task> put = async () => { await client.Child("dinos/dino1").PutAsync(ResponseData_OneDino); };
            put.ShouldNotThrow<FirebaseException>();
        }

        [TestMethod]
        public void PutAsyncShouldThrowAnExceptionBecauseTheRequestUriIsWrong()
        {
            var requestUri = new Uri(BasePath + "/dinos/WRONG_PATH/.json?print=silent");
            var client = GetFirebaseClient(ResponseData_OneDino, requestUri);

            Func<Task> put = async () => { await client.Child("dinos/dino1").PutAsync(ResponseData_OneDino); };
            put.ShouldThrow<FirebaseException>();
        }

        [TestMethod]
        public void PatchAsyncShouldNotThrowAnException()
        {
            var requestUri = new Uri(BasePath + "/dinos/dino3/.json?print=silent");
            var client = GetFirebaseClient(ResponseData_Dino3Height, requestUri);

            Func<Task> patch = async () => { await client.Child("dinos/dino3").PatchAsync(ResponseData_Dino3Height); };
            patch.ShouldNotThrow<FirebaseException>();
        }

        [TestMethod]
        public void PatchAsyncShouldThrowAnExceptionBecauseTheChildPathIsWrong()
        {
            var requestUri = new Uri(BasePath + "/dinos/dino3/.json");
            var client = GetFirebaseClient(ResponseData_Dino3Height, requestUri);

            Func<Task> patch = async () => { await client.Child("WRONG_PATH").PatchAsync(ResponseData_Dino3Height); };
            patch.ShouldThrow<FirebaseException>();
        }

        [TestMethod]
        public void OnceSingleAsyncShouldNotThrowAnException()
        {
            var requestUri = new Uri(BasePath + "/dinos/dino1/.json");
            var client = GetFirebaseClient(ResponseData_OneDino, requestUri);

            Func<Task> onceSingle = async () => { await client.Child("dinos/dino1").OnceSingleAsync<Dinosaur>(); };
            onceSingle.ShouldNotThrow<FirebaseException>();
        }

        [TestMethod]
        public void OnceSingleAsyncShouldThrowAnExceptionBecauseChildPathIsWrong()
        {
            var requestUri = new Uri(BasePath + "/dinos/dino1/.json");
            var client = GetFirebaseClient(ResponseData_OneDino, requestUri);

            Func<Task> onceSingle = async () => { await client.Child("WRONG_PATH").OnceSingleAsync<Dinosaur>(); };
            onceSingle.ShouldThrow<FirebaseException>();
        }

        [TestMethod]
        public void OnceAsyncShouldNotThrowAnException()
        {
            var requestUri = new Uri(BasePath + "/dinos/.json");
            var client = GetFirebaseClient(ResponseData_TwoDinos, requestUri);

            Func<Task> once = async () => { await client.Child("dinos").OnceAsync<Dinosaur>(); };
            once.ShouldNotThrow<FirebaseException>();
        }

        [TestMethod]
        public void OnceAsyncShouldThrowAnExceptionBecauseTheChildPathIsWrong()
        {
            var requestUri = new Uri(BasePath + "/dinos/.json");
            var client = GetFirebaseClient(ResponseData_TwoDinos, requestUri);

            Func<Task> once = async () => { await client.Child("WRONG_PATH").OnceAsync<Dinosaur>(); };
            once.ShouldThrow<FirebaseException>();
        }

        [TestMethod]
        public void DeleteAsyncShouldNotThrowAnException()
        {
            var requestUri = new Uri(BasePath + "/dinos/dino1/.json");
            var client = GetFirebaseClient(ResponseData_OneDino, requestUri);

            Func<Task> delete = async () => { await client.Child("dinos/dino1").DeleteAsync(); };
            delete.ShouldNotThrow<FirebaseException>();
        }

        [TestMethod]
        public void DeleteAsyncShouldThrowAnExceptionBecauseTheChildPathIsWrong()
        {
            var requestUri = new Uri(BasePath + "/dinos/dino1/.json");
            var client = GetFirebaseClient(ResponseData_OneDino, requestUri);

            Func<Task> delete = async () => { await client.Child("WRONG_PATH").DeleteAsync(); };
            delete.ShouldThrow<FirebaseException>();
        }

        [TestMethod]
        public void RetrieveAStreamOfTwoDinosaursUsingOnceAsyncAsAnObservable()
        {
            var client = GetFirebaseClient(ResponseData_TwoDinos);
            var stream = client.Child("dinos")
                .OnceAsync<Dinosaur>()
                .ToObservable()
                .SelectMany(x => x)
                .Select(x => x.Key);

            TestScheduler scheduler = new TestScheduler();
            ITestableObserver<string> results = scheduler.CreateObserver<string>();
            stream.Subscribe(results);
            scheduler.Start();

            results.Messages.AssertEqual(
                ReactiveTest.OnNext(0, "dino2"),
                ReactiveTest.OnNext(0, "dino3"),
                ReactiveTest.OnCompleted<string>(0));
        }

        private FirebaseClient GetFirebaseClient(string responseData, Uri requestUri = null)
        {
            var messageResponse = FakeHttpMessageHandler.GetStringHttpResponseMessage(responseData);
            var httpMsgOptions = new HttpMessageOptions
            {
                RequestUri = requestUri,
                HttpResponseMessage = messageResponse
            };

            FirebaseOptions firebaseOptions = new FirebaseOptions()
            {
                HttpMessageHandler = new FakeHttpMessageHandler(httpMsgOptions)
            };

            var client = new FirebaseClient(BasePath, firebaseOptions);

            return client;
        }
    }
}
