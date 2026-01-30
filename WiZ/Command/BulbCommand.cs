using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WiZ.Models;
using WiZ.Observable;
using WiZ.Profiles;

namespace WiZ
{
    /// <summary>
    /// Root object for the WiZ UDP command protocol.
    /// Encapsulates the method, parameters, and results of a bulb interaction.
    /// </summary>
    public sealed class BulbCommand : ObservableBase
    {
        #region Private Fields
        private string _environment;
        private BulbMethod _method = BulbMethod.GetPilot;
        private BulbParams _params;
        private BulbParams _result;
        #endregion

        #region JSON Serialization Settings
        /// <summary>
        /// Static collection of converters used for WiZ protocol serialization.
        /// </summary>
        private static List<JsonConverter> GetConverters() => new List<JsonConverter>
        {
            new TupleConverter(),
            new MACAddressConverter(),
            new ODJsonConverter<MACAddress, BulbModel>(nameof(BulbModel.MACAddress)),
            new ODJsonConverter<int, Room>(nameof(Room.RoomId)),
            new ODJsonConverter<int, Home>(nameof(Home.HomeId)),
            new ODJsonConverter<Guid, Scene>(nameof(Scene.SceneId)),
            new BulbMethodJsonConverter(),
            new IPAddressConverter()
        };

        /// <summary>
        /// Standard JSON settings for bulb communication.
        /// </summary>
        public static JsonSerializerSettings DefaultJsonSettings { get; } = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = GetConverters()
        };

        /// <summary>
        /// Indented JSON settings for profile persistence and debugging.
        /// </summary>
        public static JsonSerializerSettings DefaultProjectJsonSettings { get; } = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = GetConverters()
        };
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="BulbCommand"/> class with default parameters.
        /// </summary>
        public BulbCommand()
        {
            _params = new BulbParams();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BulbCommand"/> class with a specific <see cref="BulbMethod"/>.
        /// </summary>
        /// <param name="method">The method to execute.</param>
        public BulbCommand(BulbMethod method) : this()
        {
            _method = method;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BulbCommand"/> class by parsing a JSON string.
        /// </summary>
        /// <param name="json">The JSON command string.</param>
        public BulbCommand(string json)
        {
            
            if (!string.IsNullOrWhiteSpace(json))
            {
                JsonConvert.PopulateObject(json, this, DefaultJsonSettings);
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Optional environment identifier.
        /// </summary>
        [JsonProperty("env")]
        public string Environment
        {
            get => _environment;
            set => SetProperty(ref _environment, value);
        }

        /// <summary>
        /// The WiZ method (e.g., getPilot, setPilot, etc.).
        /// </summary>
        [JsonProperty("method")]
        public BulbMethod Method
        {
            get => _method;
            set => SetProperty(ref _method, value);
        }

        /// <summary>
        /// The input parameters for the command.
        /// </summary>
        [JsonProperty("params")]
        public BulbParams Params
        {
            get => _params;
            set => SetProperty(ref _params, value);
        }

        /// <summary>
        /// The result returned by the bulb after execution.
        /// </summary>
        [JsonProperty("result")]
        public BulbParams Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Assembles the command into a JSON string suitable for sending over UDP.
        /// </summary>
        /// <returns>A JSON string representing the command.</returns>
        public string AssembleCommand() => JsonConvert.SerializeObject(this, DefaultJsonSettings);

        /// <summary>
        /// Returns a formatted JSON string representing the current state of the command.
        /// </summary>
        public override string ToString() => JsonConvert.SerializeObject(this, DefaultProjectJsonSettings);
        #endregion

        #region Factory Methods
        /// <summary>
        /// Creates a GetPilot command.
        /// </summary>
        public static BulbCommand GetPilot() => new BulbCommand(BulbMethod.GetPilot);

        /// <summary>
        /// Creates a SetPilot command with the given settings.
        /// </summary>
        public static BulbCommand SetPilot(BulbParams settings) => new BulbCommand(BulbMethod.SetPilot) { Params = settings };

        /// <summary>
        /// Creates a GetSystemConfig command.
        /// </summary>
        public static BulbCommand GetSystemConfig() => new BulbCommand(BulbMethod.GetSystemConfig);
        #endregion
    }
}
