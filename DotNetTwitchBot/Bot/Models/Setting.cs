using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(Name))]
    public class Setting
    {
        public enum DataTypeEnum
        {
            String,
            Int,
            Double,
            Long
        }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DataTypeEnum DataType { get; set; } = DataTypeEnum.String;
        public string StringSetting { get; set; } = string.Empty;
        public int IntSetting { get; set; }
        public double DoubleSetting { get; set; }
        public long LongSetting { get; set; }
    }
}