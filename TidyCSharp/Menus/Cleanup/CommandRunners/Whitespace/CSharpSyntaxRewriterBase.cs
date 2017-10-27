﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Geeks.VSIX.TidyCSharp.Cleanup.NormalizeWhitespace
{
    public class CSharpSyntaxRewriterBase : CSharpSyntaxRewriter
    {
        protected SyntaxNode InitialSource;
        readonly WhiteSpaceNormalizerOptions Options;
        static SyntaxTrivia _endOfLineTrivia = default(SyntaxTrivia);

        public CSharpSyntaxRewriterBase(SyntaxNode initialSource, WhiteSpaceNormalizerOptions options) : base()
        {
            InitialSource = initialSource;
            Options = options;

            _endOfLineTrivia =
                initialSource
                    .SyntaxTree
                    .GetRoot()
                    .DescendantTrivia(descendIntoTrivia: true)
                    .FirstOrDefault(x => x.IsKind(SyntaxKind.EndOfLineTrivia));
        }

        protected bool CheckOption(WSN_CleanupTypes optionItem)
        {
            if (Options == null) return true;
            if (Options.CleanupItems == null) return true;

            return (Options.CleanupItems & optionItem) == optionItem;
        }

        #region

        SyntaxTriviaList CleanUpList(SyntaxTriviaList newList, WSN_CleanupTypes? option = null)
        {
            if (option.HasValue && CheckOption(option.Value) == false) return newList;

            var lineBreaksAtBeginning = newList.TakeWhile(t => t.IsKind(SyntaxKind.EndOfLineTrivia)).Count();

            if (lineBreaksAtBeginning > 1)
            {
                newList = newList.Skip(lineBreaksAtBeginning - 1).ToSyntaxTriviaList();
            }

            return newList;
        }

        SyntaxTriviaList CleanUpList(SyntaxTriviaList syntaxTrivias, int exactNumberOfBlanks)
        {
            var lineBreaksAtBeginning = syntaxTrivias.TakeWhile(t => t.IsKind(SyntaxKind.EndOfLineTrivia)).Count();

            if (lineBreaksAtBeginning > exactNumberOfBlanks)
            {
                syntaxTrivias = syntaxTrivias.Skip(lineBreaksAtBeginning - exactNumberOfBlanks)
                    .ToSyntaxTriviaList();
            }
            else if (lineBreaksAtBeginning < exactNumberOfBlanks)
            {
                var newList = syntaxTrivias.ToList();
                for (var i = lineBreaksAtBeginning; i < exactNumberOfBlanks; i++)
                {
                    newList.Insert(0, _endOfLineTrivia);
                }
                syntaxTrivias = new SyntaxTriviaList().AddRange(newList);
            }

            return syntaxTrivias;
        }

        protected SyntaxTriviaList CleanUpListWithNoWhitespaces(SyntaxTriviaList syntaxTrivias, bool itsForCloseBrace = false)
        {
            syntaxTrivias = ProcessSpecialTrivias(syntaxTrivias, itsForCloseBrace);

            var specialTriviasCount =
                syntaxTrivias
                    .Count(t =>
                        !t.IsKind(SyntaxKind.EndOfLineTrivia) && !t.IsKind(SyntaxKind.WhitespaceTrivia)
                    );

            if (specialTriviasCount > 0) return CleanUpList(syntaxTrivias);

            return CleanUpList(syntaxTrivias, 0);
        }

        protected SyntaxTriviaList CleanUpListWithDefaultWhitespaces(SyntaxTriviaList syntaxTrivias, bool itsForCloseBrace = false)
        {
            syntaxTrivias = CleanUpList(syntaxTrivias);
            syntaxTrivias = ProcessSpecialTrivias(syntaxTrivias, itsForCloseBrace);

            return syntaxTrivias;
        }

        protected SyntaxTriviaList CleanUpListWithExactNumberOfWhitespaces(SyntaxTriviaList syntaxTrivias, int exactNumberOfBlanks, bool itsForCloseBrace = false)
        {
            syntaxTrivias = CleanUpList(syntaxTrivias, exactNumberOfBlanks);
            syntaxTrivias = ProcessSpecialTrivias(syntaxTrivias, itsForCloseBrace);

            return syntaxTrivias;
        }

        protected SyntaxTriviaList ProcessSpecialTrivias(SyntaxTriviaList syntaxTrivias, bool itsForCloseBrace)
        {
            if (CheckShortSyntax(syntaxTrivias, itsForCloseBrace)) return syntaxTrivias;
            var specialTriviasCount = syntaxTrivias.Count(t => !t.IsKind(SyntaxKind.EndOfLineTrivia) && !t.IsKind(SyntaxKind.WhitespaceTrivia));

            var outputTriviasList = new List<SyntaxTrivia>();
            var specialTiviasCount = 0;
            var bAddedBlankLine = false;

            for (var i = 0; i < syntaxTrivias.Count; i++)
            {
                var countOfChars = 0;

                if (specialTiviasCount == specialTriviasCount)
                {
                    if (itsForCloseBrace)
                    {
                        if (CheckOption(WSN_CleanupTypes.Remove_BLs_after_Open_Bracket_and_Before_Close_Brackets))
                        {
                            i += RemoveBlankDuplication(syntaxTrivias, SyntaxKind.EndOfLineTrivia, i) + 1;

                            if (RemoveBlankDuplication(syntaxTrivias, SyntaxKind.WhitespaceTrivia, i) != -1)
                            {
                                outputTriviasList.Add(syntaxTrivias[i]);
                            }
                            i = syntaxTrivias.Count;
                        }
                        else
                        {
                            outputTriviasList.Add(syntaxTrivias[i]);
                        }
                        continue;
                    }
                }
                if
                (
                    (
                        syntaxTrivias[i].IsKind(SyntaxKind.EndOfLineTrivia) ||
                        syntaxTrivias[i].IsKind(SyntaxKind.WhitespaceTrivia) ||
                        syntaxTrivias[i].IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                        syntaxTrivias[i].IsKind(SyntaxKind.MultiLineCommentTrivia)
                    ) == false
                )
                {
                    outputTriviasList.Add(syntaxTrivias[i]);
                    specialTiviasCount++;
                    continue;
                }

                if (syntaxTrivias[i].IsKind(SyntaxKind.SingleLineCommentTrivia) || syntaxTrivias[i].IsKind(SyntaxKind.MultiLineCommentTrivia))
                {
                    syntaxTrivias = Insert_Space_Before_Comment_Text(syntaxTrivias, syntaxTrivias[i]);

                    outputTriviasList.Add(syntaxTrivias[i]);
                    i++;
                    if (i < syntaxTrivias.Count && syntaxTrivias[i].IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        outputTriviasList.Add(syntaxTrivias[i]);
                    }
                    specialTiviasCount++;
                    continue;
                }

                if (CheckOption(WSN_CleanupTypes.Remove_DBL_Inside_Comments) == false)
                {
                    outputTriviasList.Add(syntaxTrivias[i]);

                    continue;
                }

                if ((countOfChars = RemoveBlankDuplication(syntaxTrivias, SyntaxKind.EndOfLineTrivia, i)) != -1)
                {
                    outputTriviasList.Add(syntaxTrivias[i]);
                    i += countOfChars + 1;
                    bAddedBlankLine = true;
                }
                if ((countOfChars = RemoveBlankDuplication(syntaxTrivias, SyntaxKind.WhitespaceTrivia, i)) != -1)
                {
                    outputTriviasList.Add(syntaxTrivias[i]);
                    i += countOfChars;
                }
                else if (bAddedBlankLine)
                {
                    i--;
                }
                bAddedBlankLine = false;
            }
            return outputTriviasList.ToSyntaxTriviaList();
        }

        SyntaxTriviaList Insert_Space_Before_Comment_Text(SyntaxTriviaList syntaxTrivias, SyntaxTrivia currentTrivia)
        {
            if (CheckOption(WSN_CleanupTypes.Insert_Space_Before_Comment_Text))
            {
                if (currentTrivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                {
                    var commentText = currentTrivia.ToFullString().Trim();
                    if (commentText.Length > 2 && commentText[2] != ' ')
                    {
                        commentText = $"{commentText.Substring(0, 2)} {commentText.Substring(2)}";
                        syntaxTrivias = syntaxTrivias.Replace(currentTrivia, SyntaxFactory.Comment(commentText));
                    }
                }
            }
            return syntaxTrivias;
        }

        bool CheckShortSyntax(SyntaxTriviaList syntaxTrivias, bool itsForCloseBrace)
        {
            if (itsForCloseBrace) return false;
            if (syntaxTrivias.Count <= 1) return true;
            if (syntaxTrivias.Count > 2) return false;

            if (syntaxTrivias[0].IsKind(SyntaxKind.EndOfLineTrivia) &&
                syntaxTrivias[1].IsKind(SyntaxKind.WhitespaceTrivia))
                return true;
            if (syntaxTrivias[0].IsKind(SyntaxKind.WhitespaceTrivia) &&
                syntaxTrivias[1].IsKind(SyntaxKind.EndOfLineTrivia))
                return true;

            return false;
        }

        int RemoveBlankDuplication(SyntaxTriviaList syntaxTrivias, SyntaxKind kind, int iterationIndex)
        {
            if (iterationIndex >= syntaxTrivias.Count) return -1;

            var lineBreaksAtBeginning = syntaxTrivias.Skip(iterationIndex).TakeWhile(t => t.IsKind(kind)).Count();

            return lineBreaksAtBeginning - 1;
        }

        #endregion
    }
}