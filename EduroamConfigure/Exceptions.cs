﻿using System;
using System.Runtime.Serialization;

namespace EduroamConfigure
{
    public class EduroamAppUserException : Exception
    {
        public string UserFacingMessage { get; }

        public EduroamAppUserException(string message, string userFacingMessage = null) : base(message)
        {
#if DEBUG
            UserFacingMessage = userFacingMessage ?? ("NON-USER-FACING-MESSAGE: " + message);
#else
            UserFacingMessage = userFacingMessage ?? "NO REASON PROVIDED"; // TODO: rethink this strategy...
#endif
        }
    }



    [Serializable]
    public class InternetConnectionException : Exception
    {
        private const string DefaultMessage = "Error with internet connection";

        public InternetConnectionException() : base(DefaultMessage) { }
        public InternetConnectionException(string message) : base(message) { }
        public InternetConnectionException(string message, Exception innerException) : base(message, innerException) { }
        protected InternetConnectionException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    }

    [Serializable]
    public class ApiException : Exception
    {
        private const string DefaultMessage = "Error with Api";

        public ApiException() : base(DefaultMessage) { }
        public ApiException(string message) : base(message) { }
        public ApiException(string message, Exception innerException) : base(message, innerException) { }
        protected ApiException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    }

    [Serializable]
    public class ApiUnreachableException : ApiException
    {
        private const string DefaultMessage = "Api could not be reached";
        
        public ApiUnreachableException() : base(DefaultMessage) { }
        public ApiUnreachableException(string message) : base(message) { }
        public ApiUnreachableException(string message, Exception innerException) : base(message, innerException) { }

        protected ApiUnreachableException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class ApiParsingException : Exception
    {
        private const string DefaultMessage = "Api response could not be parsed";

        public ApiParsingException() : base(DefaultMessage) { }
        public ApiParsingException(string message) : base(message) { }
        public ApiParsingException(string message, Exception innerException) : base(message, innerException) { }

        protected ApiParsingException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}

