// Guids.cs
// MUST match guids.h
using System;

namespace BISim.Technologicator
{
    static class GuidList
    {
        public const string guidTechnologicatorPkgString = "97b299aa-f3d1-4c5d-bf2f-918731fffb9a";
        public const string guidTechnologicatorCmdSetString = "1a20bab1-9d91-46e2-baa0-9b74ea23835d";

        public static readonly Guid guidTechnologicatorCmdSet = new Guid(guidTechnologicatorCmdSetString);
    };
}