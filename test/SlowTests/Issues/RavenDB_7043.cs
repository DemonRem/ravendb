﻿using System;
using System.Linq;
using System.Threading;
using FastTests;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Exceptions;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_7043 : RavenTestBase
    {
        [Fact]
        public void Should_mark_index_as_errored_and_throw_on_querying_it_even_its_small()
        {
            using (var store = GetDocumentStore())
            {
                var failingIndex = new Failing_index();
                failingIndex.Execute(store);

                var count = IndexFailureInformation.SufficientNumberOfAttemptsToCheckFailureRate / 2;

                using (var session = store.OpenSession())
                {
                    
                    for (int i = 0; i < count; i++)
                    {
                        var entity = new User();

                        if (i == 0)
                            entity.Age = 1;

                        session.Store(entity);
                    }

                    session.SaveChanges();
                }

                WaitForIndexing(store, allowErrors: true);
                
                SpinWait.SpinUntil(() => store.Maintenance.Send(new GetIndexStatisticsOperation(failingIndex.IndexName)).State == IndexState.Error, TimeSpan.FromSeconds(15));

                Assert.True(store.Maintenance.Send(new GetIndexStatisticsOperation(failingIndex.IndexName)).IsInvalidIndex);

                using (var session = store.OpenSession())
                {
                    var ex = Assert.Throws<RavenException>(() =>session.Query<User, Failing_index>().ToList());
                    
                    Assert.Contains($"is invalid, out of {count} map attempts, {count - 1} has failed.", ex.Message);
                }
            }
        }

        [Fact]
        public void Should_mark_index_as_errored_and_throw_on_querying_it_even_its_very_small_and_everything_fails()
        {
            using (var store = GetDocumentStore())
            {
                var failingIndex = new Failing_index();
                failingIndex.Execute(store);

                var count = IndexFailureInformation.MinimalNumberOfAttemptsToCheckFailureRate;

                using (var session = store.OpenSession())
                {

                    for (int i = 0; i < count; i++)
                    {
                        var entity = new User();

                        session.Store(entity);
                    }

                    session.SaveChanges();
                }                               

                //we wait until the index is corrupted or 15 seconds pass
                //-> no need to WaitForIndexing() because either the index is corrupted or not, 15 seconds for 10 docs is A LOT
                SpinWait.SpinUntil(() => store.Maintenance.Send(new GetIndexStatisticsOperation(failingIndex.IndexName)).State == IndexState.Error, TimeSpan.FromSeconds(15));

                Assert.True(store.Maintenance.Send(new GetIndexStatisticsOperation(failingIndex.IndexName)).IsInvalidIndex);

                using (var session = store.OpenSession())
                {
                    var ex = Assert.Throws<RavenException>(() => session.Query<User, Failing_index>().ToList());

                    Assert.Contains($"is invalid, out of {count} map attempts, {count} has failed.", ex.Message);
                }
            }
        }

        [Fact]
        public void Should_not_mark_index_as_errored_when_there_is_low_number_of_docs_to_index_but_not_everything_fails()
        {
            using (var store = GetDocumentStore())
            {
                var failingIndex = new Failing_index();
                failingIndex.Execute(store);

                using (var session = store.OpenSession())
                {
                    for (int i = 0; i < IndexFailureInformation.MinimalNumberOfAttemptsToCheckFailureRate - 1; i++)
                    {
                        var entity = new User();

                        if (i == 0)
                            entity.Age = 1;

                        session.Store(entity);
                    }

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    session.Query<User, Failing_index>().ToList();
                }

                var indexStats = store.Maintenance.Send(new GetIndexStatisticsOperation(failingIndex.IndexName));

                Assert.False(indexStats.IsInvalidIndex);
                Assert.NotEqual(IndexState.Error, indexStats.State);
            }
        }

        private class Failing_index : AbstractIndexCreationTask<User>
        {
            public Failing_index()
            {
                Map = users => from u in users
                    select new { a = 10 / u.Age };
            }
        }
    }
}
