﻿using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System.Collections.Generic;

namespace OdataToEntity.Cache
{
    public sealed class OeCacheContext
    {
        public OeCacheContext(OeQueryContext queryContext)
        {
            ODataUri = queryContext.ODataUri;
            EntitySet = queryContext.EntitySet;
            ParseNavigationSegments = queryContext.ParseNavigationSegments;
            MetadataLevel = queryContext.MetadataLevel;
            NavigationNextLink = queryContext.NavigationNextLink;
            SkipTokenNameValues = queryContext.SkipTokenNameValues;
        }
        public OeCacheContext(OeQueryContext queryContext, IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> constantToParameterMapper)
            : this(queryContext)
        {
            ConstantToParameterMapper = constantToParameterMapper;
        }


        public IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> ConstantToParameterMapper { get; }
        public IEdmEntitySet EntitySet { get; }
        public OeMetadataLevel MetadataLevel { get; }
        public bool NavigationNextLink { get; }
        public ODataUri ODataUri { get; }
        public IReadOnlyList<OeQueryCacheDbParameterValue> ParameterValues { get; set; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments { get; }
        public OeSkipTokenNameValue[] SkipTokenNameValues { get; }
    }
}
