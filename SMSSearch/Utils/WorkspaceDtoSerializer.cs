using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AvalonDock.Core;
using AvalonDock.Core.Serialization.Dto;

namespace SMS_Search.Utils
{
    public sealed class WorkspaceDtoSerializer : LayoutSerializerBase
    {
        private static readonly Type[] ExtraDtoTypes = typeof(LayoutRootDto).Assembly
            .GetTypes()
            .Where(t => typeof(LayoutElementDto).IsAssignableFrom(t) && t != typeof(LayoutRootDto) && !t.IsAbstract)
            .ToArray();

        private static readonly XmlSerializer DtoXmlSerializer = new(typeof(LayoutRootDto), ExtraDtoTypes);

        public WorkspaceDtoSerializer(IDockingManager manager) : base(manager)
        {
        }

        protected override void SerializeCore(Stream stream, LayoutRootDto layout)
        {
            DtoXmlSerializer.Serialize(stream, layout);
        }

        protected override LayoutRootDto? DeserializeCore(Stream stream)
        {
            return DtoXmlSerializer.Deserialize(stream) as LayoutRootDto;
        }

        public static string SerializeDtoToXml(LayoutRootDto dto)
        {
            using var sw = new StringWriter();
            DtoXmlSerializer.Serialize(sw, dto);
            return sw.ToString();
        }

        public static LayoutRootDto? DeserializeDtoFromXml(string xml)
        {
            using var sr = new StringReader(xml);
            return DtoXmlSerializer.Deserialize(sr) as LayoutRootDto;
        }
    }
}

