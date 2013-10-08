﻿/* Copyright 2010-2013 10gen Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Xml;
using MongoDB.Bson;
using MongoDB.Driver.Core.Security;
using MongoDB.Driver.Core.Support;

namespace MongoDB.Driver.Core.Configuration
{
    /// <summary>
    /// A connection string.
    /// </summary>
    public sealed class DbConnectionString
    {
        // private fields
        private readonly string _originalConnectionString;
        private readonly NameValueCollection _allOptions;
        private readonly NameValueCollection _unknownOptions;

        // these are all readonly, but since they are not assigned 
        // from the ctor, they cannot be marked as such.
        private string _authMechanism;
        private string _authSource;
        private TimeSpan? _connectTimeout;
        private string _databaseName;
        private bool? _fsync;
        private IEnumerable<DnsEndPoint> _hosts;
        private bool? _ipv6;
        private bool? _journal;
        private TimeSpan? _maxIdleTime;
        private TimeSpan? _maxLifeTime;
        private int? _maxPoolSize;
        private int? _minPoolSize;
        private string _password;
        private ReadPreferenceMode? _readPreference;
        private IEnumerable<ReplicaSetTagSet> _readPreferenceTags;
        private string _replicaSet;
        private TimeSpan? _secondaryAcceptableLatency;
        private TimeSpan? _socketTimeout;
        private bool? _ssl;
        private bool? _sslVerifyCertificate;
        private string _username;
        private GuidRepresentation? _uuidRepresentation;
        private int? _waitQueueMultiple;
        private TimeSpan? _waitQueueTimeout;
        private WriteConcern.WValue _w;
        private TimeSpan? _wTimeout;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="DbConnectionString" /> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public DbConnectionString(string connectionString)
        {
            Ensure.IsNotNull("connectionString", connectionString);

            _originalConnectionString = connectionString;
            _allOptions = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
            _unknownOptions = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
            Parse();
        }

        // public properties
        /// <summary>
        /// Gets all the option names.
        /// </summary>
        public IEnumerable<string> AllOptionNames
        {
            get { return _allOptions.AllKeys; }
        }

        /// <summary>
        /// Gets all the unknown option names.
        /// </summary>
        public IEnumerable<string> AllUnknownOptionNames
        {
            get { return _unknownOptions.AllKeys; }
        }

        /// <summary>
        /// Gets the auth mechanism.
        /// </summary>
        public string AuthMechanism
        {
            get { return _authMechanism; }
        }

        /// <summary>
        /// Gets the auth source.
        /// </summary>
        public string AuthSource
        {
            get { return _authSource; }
        }

        /// <summary>
        /// Gets the connect timeout.
        /// </summary>
        public TimeSpan? ConnectTimeout
        {
            get { return _connectTimeout; }
        }

        /// <summary>
        /// Gets the name of the database.
        /// </summary>
        public string DatabaseName
        {
            get { return _databaseName; }
        }

        /// <summary>
        /// Gets the fsync.
        /// </summary>
        public bool? FSync
        {
            get { return _fsync; }
        }

        /// <summary>
        /// Gets the hosts.
        /// </summary>
        public IEnumerable<DnsEndPoint> Hosts
        {
            get { return _hosts; }
        }

        /// <summary>
        /// Gets the IPV6 value.
        /// </summary>
        public bool? Ipv6
        {
            get { return _ipv6; }
        }

        /// <summary>
        /// Gets the journal value.
        /// </summary>
        public bool? Journal
        {
            get { return _journal; }
        }

        /// <summary>
        /// Gets the max idle time.
        /// </summary>
        public TimeSpan? MaxIdleTime
        {
            get { return _maxIdleTime; }
        }

        /// <summary>
        /// Gets the max life time.
        /// </summary>
        public TimeSpan? MaxLifeTime
        {
            get { return _maxLifeTime; }
        }

        /// <summary>
        /// Gets the size of the max pool.
        /// </summary>
        public int? MaxPoolSize
        {
            get { return _maxPoolSize; }
        }

        /// <summary>
        /// Gets the size of the min pool.
        /// </summary>
        public int? MinPoolSize
        {
            get { return _minPoolSize; }
        }

        /// <summary>
        /// Gets the password.
        /// </summary>
        public string Password
        {
            get { return _password; }
        }

        /// <summary>
        /// Gets the read preference.
        /// </summary>
        public ReadPreferenceMode? ReadPreference
        {
            get { return _readPreference; }
        }

        /// <summary>
        /// Gets the replica set.
        /// </summary>
        public string ReplicaSet
        {
            get { return _replicaSet; }
        }

        /// <summary>
        /// Gets the read preference tags.
        /// </summary>
        public IEnumerable<ReplicaSetTagSet> ReadPreferenceTags
        {
            get { return _readPreferenceTags; }
        }

        /// <summary>
        /// Gets the secondary acceptable latency.
        /// </summary>
        public TimeSpan? SecondaryAcceptableLatency
        {
            get { return _secondaryAcceptableLatency; }
        }

        /// <summary>
        /// Gets the socket timeout.
        /// </summary>
        public TimeSpan? SocketTimeout
        {
            get { return _socketTimeout; }
        }

        /// <summary>
        /// Gets the SSL.
        /// </summary>
        public bool? Ssl
        {
            get { return _ssl; }
        }

        /// <summary>
        /// Gets the SSL verify certificate.
        /// </summary>
        public bool? SslVerifyCertificate
        {
            get { return _sslVerifyCertificate; }
        }

        /// <summary>
        /// Gets the username.
        /// </summary>
        public string Username
        {
            get { return _username; }
        }

        /// <summary>
        /// Gets the UUID representation.
        /// </summary>
        public GuidRepresentation? UuidRepresentation
        {
            get { return _uuidRepresentation; }
        }

        /// <summary>
        /// Gets the wait queue multiple.
        /// </summary>
        public int? WaitQueueMultiple
        {
            get { return _waitQueueMultiple; }
        }

        /// <summary>
        /// Gets the wait queue timeout.
        /// </summary>
        public TimeSpan? WaitQueueTimeout
        {
            get { return _waitQueueTimeout; }
        }

        /// <summary>
        /// Gets the w.
        /// </summary>
        public WriteConcern.WValue W
        {
            get { return _w; }
        }

        /// <summary>
        /// Gets the W timeout.
        /// </summary>
        public TimeSpan? WTimeout
        {
            get { return _wTimeout; }
        }

        // public methods
        /// <summary>
        /// Gets the option.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The option with the specified name.</returns>
        public string GetOption(string name)
        {
            return _allOptions[name];
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return _originalConnectionString;
        }

        // private methods
        private void ExtractDatabaseName(Match match)
        {
            var databaseGroup = match.Groups["database"];
            if (databaseGroup.Success)
            {
                _databaseName = databaseGroup.Value;
            }
        }

        private void ExtractHosts(Match match)
        {
            List<DnsEndPoint> dnsEndPoints = new List<DnsEndPoint>();
            foreach (Capture host in match.Groups["host"].Captures)
            {
                DnsEndPoint dnsEndPoint;
                if (DnsEndPointParser.TryParse(host.Value, AddressFamily.Unspecified, out dnsEndPoint))
                {
                    dnsEndPoints.Add(dnsEndPoint);
                }
                else
                {
                    throw new MongoConfigurationException(string.Format("Host '{0}' is not valid.", host.Value));
                }
            }

            _hosts = dnsEndPoints;
        }

        private void ExtractOptions(Match match)
        {
            foreach (Capture option in match.Groups["option"].Captures)
            {
                var parts = option.Value.Split('=');
                _allOptions.Add(parts[0], parts[1]);
                ParseOption(parts[0], parts[1]);
            }
        }

        private void ExtractUsernameAndPassword(Match match)
        {
            var usernameGroup = match.Groups["username"];
            if (usernameGroup.Success)
            {
                _username = Uri.UnescapeDataString(usernameGroup.Value);
            }

            var passwordGroup = match.Groups["password"];
            if (passwordGroup.Success)
            {
                _password = Uri.UnescapeDataString(passwordGroup.Value);
            }
        }

        private void Parse()
        {
            const string serverPattern = @"(?<host>((\[[^]]+?\]|[^:,/?#]+)(:\d+)?))";
            const string optionPattern = @"(?<option>[^&;]+=[^&;]+)";
            const string pattern =
                @"^mongodb://" +
                @"((?<username>[^:@]+)(:(?<password>[^@]+))?@)?" +
                serverPattern + @"(," + serverPattern + ")*" +
                @"(/(?<database>[^/?]+))?" +
                @"/?(\?" + optionPattern + @"((&|;)" + optionPattern + ")*)?$";

            var match = Regex.Match(_originalConnectionString, pattern);
            if (!match.Success)
            {
                var message = string.Format("The connection string '{0}' is not valid.", _originalConnectionString);
                throw new MongoConfigurationException(message);
            }

            ExtractUsernameAndPassword(match);
            ExtractHosts(match);
            ExtractDatabaseName(match);
            ExtractOptions(match);
        }

        private void ParseOption(string name, string value)
        {
            switch (name.ToLower())
            {
                case "authmechanism":
                    _authMechanism = value;
                    break;
                case "authsource":
                    _authSource = value;
                    break;
                case "connecttimeout":
                case "connecttimeoutms":
                    _connectTimeout = GetTimeSpan(name, value);
                    break;
                case "fsync":
                    _fsync = GetBoolean(name, value);
                    break;
                case "ipv6":
                    _ipv6 = GetBoolean(name, value);
                    break;
                case "j":
                case "journal":
                    _journal = GetBoolean(name, value);
                    break;
                case "maxidletime":
                case "maxidletimems":
                    _maxIdleTime = GetTimeSpan(name, value);
                    break;
                case "maxlifetime":
                case "maxlifetimems":
                    _maxLifeTime = GetTimeSpan(name, value);
                    break;
                case "maxpoolsize":
                    _maxPoolSize = GetInt32(name, value);
                    break;
                case "minpoolsize":
                    _minPoolSize = GetInt32(name, value);
                    break;
                case "readpreference":
                    _readPreference = GetEnum<ReadPreferenceMode>(name, value);
                    break;
                case "readpreferencetags":
                    var tags = GetReadPreferenceTagSets(name, value);
                    if (_readPreferenceTags == null)
                    {
                        _readPreferenceTags = new List<ReplicaSetTagSet> { tags }.AsReadOnly();
                    }
                    else
                    {
                        _readPreferenceTags = _readPreferenceTags.Concat(new[] { tags });
                    }
                    break;
                case "replicaset":
                    _replicaSet = value;
                    break;
                case "secondaryacceptablelatency":
                case "secondaryacceptablelatencyms":
                    _secondaryAcceptableLatency = GetTimeSpan(name, value);
                    break;
                case "sockettimeout":
                case "sockettimeoutms":
                    _socketTimeout = GetTimeSpan(name, value);
                    break;
                case "ssl":
                    _ssl = GetBoolean(name, value);
                    break;
                case "sslverifycertificate":
                    _sslVerifyCertificate = GetBoolean(name, value);
                    break;
                case "guids":
                case "uuidrepresentation":
                    _uuidRepresentation = GetEnum<GuidRepresentation>(name, value);
                    break;
                case "w":
                    _w = WriteConcern.WValue.Parse(value);
                    break;
                case "wtimeout":
                case "wtimeoutms":
                    _wTimeout = GetTimeSpan(name, value);
                    break;
                case "waitqueuemultiple":
                    _waitQueueMultiple = GetInt32(name, value);
                    break;
                case "waitqueuetimeout":
                case "waitqueuetimeoutms":
                    _waitQueueTimeout = GetTimeSpan(name, value);
                    break;
                default:
                    _unknownOptions.Add(name, value);
                    break;
            }
        }

        // private static methods
        private static bool GetBoolean(string name, string value)
        {
            try
            {
                return XmlConvert.ToBoolean(value.ToLower());
            }
            catch (Exception ex)
            {
                throw new MongoConfigurationException(string.Format("{0} has an invalid boolean value of {1}.", name, value), ex);
            }
        }

        private static TEnum GetEnum<TEnum>(string name, string value)
            where TEnum : struct
        {
            try
            {
                return (TEnum)Enum.Parse(typeof(TEnum), value, true);
            }
            catch (Exception ex)
            {
                throw new MongoConfigurationException(string.Format("{0} has an invalid {1} value of {2}.", name, typeof(TEnum), value), ex);
            }
        }

        private static int GetInt32(string name, string value)
        {
            try
            {
                return XmlConvert.ToInt32(value);
            }
            catch (Exception ex)
            {
                throw new MongoConfigurationException(string.Format("{0} has an invalid int32 value of {1}.", name, value), ex);
            }
        }

        private static ReplicaSetTagSet GetReadPreferenceTagSets(string name, string value)
        {
            var tagSet = new ReplicaSetTagSet();
            foreach (var tagString in value.Split(','))
            {
                var parts = tagString.Split(':');
                if (parts.Length != 2)
                {
                    throw new MongoConfigurationException(string.Format("{0} has an invalid value of {1}.", name, value));
                }
                var tag = new ReplicaSetTag(parts[0].Trim(), parts[1].Trim());
                tagSet.Add(tag);
            }
            return tagSet;
        }

        private static TimeSpan GetTimeSpan(string name, string value)
        {
            // all timespan keys can be suffixed with 'MS'
            var lowerName = name.ToLower();
            var lowerValue = value.ToLower();
            var end = lowerValue.Length - 1;

            var multiplier = 1000; // default units are seconds
            if (lowerName.EndsWith("ms", StringComparison.Ordinal))
            {
                multiplier = 1;
            }
            else if (lowerValue.EndsWith("ms", StringComparison.Ordinal))
            {
                lowerValue = lowerValue.Substring(0, lowerValue.Length - 2);
                multiplier = 1;
            }
            else if (lowerValue[end] == 's')
            {
                lowerValue = lowerValue.Substring(0, lowerValue.Length - 1);
                multiplier = 1000;
            }
            else if (lowerValue[end] == 'm')
            {
                lowerValue = lowerValue.Substring(0, lowerValue.Length - 1);
                multiplier = 60 * 1000;
            }
            else if (lowerValue[end] == 'h')
            {
                lowerValue = lowerValue.Substring(0, lowerValue.Length - 1);
                multiplier = 60 * 60 * 1000;
            }
            else if (lowerValue.IndexOf(':') != -1)
            {
                try
                {
                    return TimeSpan.Parse(lowerValue);
                }
                catch (Exception ex)
                {
                    throw new MongoConfigurationException(string.Format("{0} has an invalid TimeSpan value of {1}.", name, value), ex);
                }
            }

            try
            {
                return TimeSpan.FromMilliseconds(multiplier * XmlConvert.ToDouble(lowerValue));
            }
            catch (Exception ex)
            {
                throw new MongoConfigurationException(string.Format("{0} has an invalid TimeSpan value of {1}.", name, value), ex);
            }
        }
    }
}