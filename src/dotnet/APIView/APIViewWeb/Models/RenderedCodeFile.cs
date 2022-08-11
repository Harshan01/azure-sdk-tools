﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using ApiView;
using APIView.Model;

namespace APIViewWeb.Models
{
    public class RenderedCodeFile
    {
        private CodeLine[] _rendered;
        private CodeLine[] _renderedReadOnly;
        private CodeLine[] _renderedText;

        public RenderedCodeFile(CodeFile codeFile)
        {
            CodeFile = codeFile;
        }

        public CodeFile CodeFile { get; }

        public RenderResult RenderResult { get; private set; }

        public CodeLine[] Render(bool showDocumentation)
        {
            //Always render when documentation is requested to avoid cach thrashing
            if (showDocumentation)
            {
                RenderResult =  CodeFileHtmlRenderer.Normal.Render(CodeFile, showDocumentation: true);
                return RenderResult.CodeLines;
            }

            if (_rendered == null)
            {
                RenderResult = CodeFileHtmlRenderer.Normal.Render(CodeFile);
                _rendered = RenderResult.CodeLines;
            }

            return _rendered;
        }

        public CodeLine[] RenderReadOnly(bool showDocumentation)
        {
            if (showDocumentation)
            {
                RenderResult = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile, showDocumentation: true);
                return RenderResult.CodeLines;
            }

            if (_renderedReadOnly == null)
            {
                RenderResult = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile);
                _renderedReadOnly = RenderResult.CodeLines;
            }

            return _renderedReadOnly;
        }

        internal CodeLine[] RenderText(bool showDocumentation, bool skipDiff = false)
        {
            if (showDocumentation || skipDiff)
            {
                RenderResult = CodeFileRenderer.Instance.Render(CodeFile, showDocumentation: showDocumentation, enableSkipDiff: skipDiff);
                return RenderResult.CodeLines;
            }

            if (_renderedText == null)
            {
                RenderResult = CodeFileRenderer.Instance.Render(CodeFile);
                _renderedText = RenderResult.CodeLines;
            }

            return _renderedText;
        }

        public CodeLine[] GetCodeLineSection(int sectionId)
        {
            var section = RenderResult.Sections[sectionId];
            var result = new List<CodeLine>();

            using (IEnumerator<TreeNode<CodeLine>> enumerator = section.GetEnumerator())
            {
                enumerator.MoveNext();
                while (enumerator.MoveNext())
                {
                    var node = enumerator.Current;
                    var lineClass = new List<string>();

                    // Add classes for managing tree hierachy
                    if (node.Children.Count > 0)
                        lineClass.Add($"lvl_{node.Level}_Parent");

                    if (!node.IsRoot)
                        lineClass.Add($"lvl_{node.Level}_Child");

                    var lineClasses = String.Join(' ', lineClass);

                    if (!String.IsNullOrWhiteSpace(node.Data.LineClass))
                        lineClasses = node.Data.LineClass.Trim() + $" {lineClasses}";

                    if (node.IsLeaf)
                    {
                        int leafSectionId;
                        bool parseWorked = Int32.TryParse(node.Data.DisplayString, out leafSectionId);

                        if (parseWorked && CodeFile.LeafSections.Count > leafSectionId)
                        {
                            var leafSection = CodeFile.LeafSections[leafSectionId];
                            var renderedLeafSection = CodeFileHtmlRenderer.Normal.Render(leafSection);
                            foreach (var codeLine in renderedLeafSection)
                            {
                                if (!String.IsNullOrWhiteSpace(codeLine.LineClass))
                                {
                                    lineClasses = codeLine.LineClass.Trim() + $" {lineClasses}";
                                }
                                result.Add(new CodeLine(codeLine, lineClass: lineClasses));
                            }
                        }
                        else
                        {
                            result.Add(new CodeLine(node.Data, lineClasses));
                        }
                    }
                    else 
                    {
                        result.Add(new CodeLine(node.Data, lineClasses));
                    }
                }
            }
            return result.ToArray();
        }
    }
}
