using System;

namespace Xpk.Core
{
    /// <summary>
    /// An expected failure with a stable error code (e.g. UNKNOWN_PROPERTY, XPACK_NOT_FOUND) and a message for the user (Dutch).
    /// CLI and MCP turn it into { ok: false, error: { code, message } }.
    /// </summary>
    public sealed class XpkException : Exception
    {
        public XpkException(string code, string message, Exception inner = null)
            : base(message, inner)
        {
            Code = code;
        }

        public string Code { get; }
    }

    /// <summary>Error codes shared by Core, CLI and MCP.</summary>
    public static class ErrorCodes
    {
        public const string ConfigInvalid = "CONFIG_INVALID";
        public const string XPackNotFound = "XPACK_NOT_FOUND";
        public const string NotFound = "NOT_FOUND";
        public const string PathTooLong = "PATH_TOO_LONG";
        public const string FileNotLocal = "FILE_NOT_LOCAL";
        public const string LoadFailed = "LOAD_FAILED";
        public const string RaceBitsTimeout = "RACEBITS_TIMEOUT";
        public const string RaceBitsDialog = "RACEBITS_DIALOG";
        public const string UnknownProperty = "UNKNOWN_PROPERTY";
        public const string InvalidValue = "INVALID_VALUE";
        public const string InvalidArguments = "INVALID_ARGUMENTS";
    }
}
