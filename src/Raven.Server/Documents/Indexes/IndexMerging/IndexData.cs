﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Raven.Client.Documents.Indexes;

namespace Raven.Server.Documents.Indexes.IndexMerging
{
    internal class IndexData
    {
        private readonly IndexDefinition _index;
        public Dictionary<string, ExpressionSyntax> SelectExpressions = new Dictionary<string, ExpressionSyntax>();

        public IndexData(IndexDefinition index)
        {
            _index = index;
        }

        public ExpressionSyntax FromExpression { get; set; }
        public string FromIdentifier { get; set; }
        public int NumberOfFromClauses { get; set; }
        public int NumberOfSelectClauses { get; set; }

        public HashSet<string> OriginalMaps { get; set; }
        public bool HasWhere { get; set; }
        public bool HasLet { get; set; }
        public bool HasGroup { get; set; }
        public bool HasOrder { get; set; }

        public string IndexName { get; set; }
        public bool IsAlreadyMerged { get; set; }
        public bool IsSuitedForMerge { get; set; }
        public string Comment { get; set; }

        public string Collection { get; set; }
        public InvocationExpressionSyntax InvocationExpression { get; set; }
        public IndexDefinition Index => _index;
        public bool IsMapReduceOrMultiMap { get; set; }

        public string BuildExpression(Dictionary<string, ExpressionSyntax> selectExpressions)
        {
            var memberDeclarators = new SeparatedSyntaxList<AnonymousObjectMemberDeclaratorSyntax>();
            AnonymousObjectCreationExpressionSyntax anonymousObjectCreationExpression = null;

            foreach (var curExpr in selectExpressions.OrderBy(x => x.Key))
            {
                var name = SyntaxFactory.NameEquals(curExpr.Key);
                var assignmentExpression = SyntaxFactory.AnonymousObjectMemberDeclarator(name, curExpr.Value);
                memberDeclarators = memberDeclarators.Add(assignmentExpression);
            }

            anonymousObjectCreationExpression = SyntaxFactory.AnonymousObjectCreationExpression(memberDeclarators);

            if (InvocationExpression != null)
            {
                var memberAccessExpression = InvocationExpression.Expression as MemberAccessExpressionSyntax;
                var invocExp = SyntaxFactory.InvocationExpression(memberAccessExpression)
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(
                                    SyntaxFactory.SimpleLambdaExpression(
                                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("doc")),
                                        anonymousObjectCreationExpression)))));
                return invocExp.ToString().Normalize();
            }

            if (FromExpression != null)
            {
                var queryExpr = SyntaxFactory.QueryExpression(
                    SyntaxFactory.FromClause("doc", FromExpression),
                    SyntaxFactory.QueryBody(SyntaxFactory.SelectClause(anonymousObjectCreationExpression)));
                return queryExpr.ToString().Normalize();
            }

            return null;

        }

        public override string ToString()
        {
            return string.Format("IndexName: {0}", IndexName);
        }
    }

}
