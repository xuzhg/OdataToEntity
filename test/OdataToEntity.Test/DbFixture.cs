﻿using Microsoft.EntityFrameworkCore;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixture
    {
        private readonly String _databaseName;
        private readonly bool _useRelationalNulls;

        public DbFixture(bool allowCache, bool useRelationalNulls)
        {
            _useRelationalNulls = useRelationalNulls;

            _databaseName = Model.OrderContext.GenerateDatabaseName();
            DbDataAdapter = new OrderDbDataAdapter(allowCache, useRelationalNulls, _databaseName);
            OeDataAdapter = new OrderOeDataAdapter(allowCache, useRelationalNulls, _databaseName);

            EdmModel = OeDataAdapter.BuildEdmModel();
        }

        public Model.OrderContext CreateContext()
        {
            return new Model.OrderContext(Model.OrderContextOptions.Create(_useRelationalNulls, _databaseName));
        }
        public virtual async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, false, 0).ConfigureAwait(false);
            IList fromDb;
            using (var dataContext = (DbContext)DbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(DbDataAdapter.EntitySetAdapters, dataContext, parameters.Expression);

            Console.WriteLine(parameters.RequestUri);
            TestHelper.Compare(fromDb, fromOe, DbDataAdapter.EntitySetAdapters.EdmModelMetadataProvider, null);
        }
        public virtual async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            IList fromOe = await ExecuteOe<TResult>(parameters.RequestUri, parameters.NavigationNextLink, parameters.PageSize).ConfigureAwait(false);
            IList fromDb;
            IReadOnlyList<IncludeVisitor.Include> includes;
            using (var dataContext = (DbContext)DbDataAdapter.CreateDataContext())
                fromDb = TestHelper.ExecuteDb(DbDataAdapter, dataContext, parameters.Expression, out includes);

            Console.WriteLine(parameters.RequestUri);
            TestHelper.Compare(fromDb, fromOe, DbDataAdapter.EntitySetAdapters.EdmModelMetadataProvider, includes);
        }
        internal async Task ExecuteBatchAsync(String batchName)
        {
            var parser = new OeParser(new Uri("http://dummy/"), OeDataAdapter, EdmModel);
            String fileName = Directory.EnumerateFiles(".", batchName + ".batch", SearchOption.AllDirectories).First();
            byte[] bytes = File.ReadAllBytes(fileName);
            var responseStream = new MemoryStream();

            await parser.ExecuteBatchAsync(new MemoryStream(bytes), responseStream, CancellationToken.None).ConfigureAwait(false);
        }
        public async Task<IList> ExecuteOe<TResult>(String requestUri, bool navigationNextLink, int pageSize)
        {
            ODataUri odataUri = ParseUri(requestUri);
            var parser = new OeParser(odataUri.ServiceRoot, OeDataAdapter, EdmModel);
            var uri = new Uri(odataUri.ServiceRoot, requestUri);
            OeRequestHeaders requestHeaders = OeRequestHeaders.JsonDefault.SetMaxPageSize(pageSize).SetNavigationNextLink(navigationNextLink);

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, requestHeaders, response, CancellationToken.None).ConfigureAwait(false);
                response.Position = 0;

                List<Object> result;
                ResponseReader responseReader;
                if (typeof(TResult).IsPrimitive)
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(TResult));
                    return new Object[] { converter.ConvertFromString(new StreamReader(response).ReadToEnd()) };
                }
                else if (typeof(TResult) == typeof(Object))
                {
                    responseReader = new OpenTypeResponseReader(EdmModel, DbDataAdapter);
                    result = responseReader.Read(response).Cast<Object>().ToList();
                }
                else
                {
                    responseReader = new ResponseReader(EdmModel, DbDataAdapter);
                    result = responseReader.Read<TResult>(response).Cast<Object>().ToList();
                }

                if (pageSize > 0)
                    Xunit.Assert.InRange(result.Count, 0, requestHeaders.MaxPageSize);
                fromOe.AddRange(result);

                foreach (Object entity in fromOe)
                    await responseReader.FillNextLinkProperties(parser, entity, CancellationToken.None).ConfigureAwait(false);

                if (count < 0)
                    count = responseReader.ResourceSet.Count.GetValueOrDefault();

                uri = responseReader.ResourceSet.NextPageLink;
            }
            while (uri != null);

            if (odataUri.QueryCount != null)
                Xunit.Assert.Equal(count, fromOe.Count);

            return fromOe;
        }
        public abstract Task Initalize();
        public ODataUri ParseUri(String requestRelativeUri)
        {
            var baseUri = new Uri("http://dummy/");
            var odataParser = new ODataUriParser(EdmModel, baseUri, new Uri(baseUri, requestRelativeUri));
            odataParser.Resolver.EnableCaseInsensitive = true;
            return odataParser.ParseUri();
        }

        internal OrderDbDataAdapter DbDataAdapter { get; }
        internal EdmModel EdmModel { get; }
        internal OrderOeDataAdapter OeDataAdapter { get; }
    }
}
