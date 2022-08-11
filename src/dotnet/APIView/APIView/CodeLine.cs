﻿using System;

namespace ApiView
{
    public readonly struct CodeLine: IEquatable<CodeLine>
    {
        public string DisplayString { get; }
        public string ElementId { get; }
        public string LineClass { get; }
        public int? LineNumber { get; }
        public int? SectionKey { get;  }

        public CodeLine(string html, string id, string lineClass, int? lineNumber = null, int? sectionKey = null)
        {
            this.DisplayString = html;
            this.ElementId = id;
            this.LineClass = lineClass;
            this.LineNumber = lineNumber;
            this.SectionKey = sectionKey;
        }

        public CodeLine(CodeLine codeLine, string html = null, string id = null, string lineClass = null, int? lineNumber = null, int? sectionKey = null)
        {
            this.DisplayString = html ?? codeLine.DisplayString;
            this.ElementId = id ?? codeLine.ElementId;
            this.LineClass = lineClass ?? codeLine.LineClass;
            this.LineNumber = lineNumber ?? codeLine.LineNumber;
            this.SectionKey = sectionKey ?? codeLine.SectionKey;
        }

        public override string ToString()
        {
            return DisplayString + " [" + ElementId + "]";
        }

        public bool Equals(CodeLine other)
        {
            return DisplayString == other.DisplayString && ElementId == other.ElementId;
        }
    }
}
