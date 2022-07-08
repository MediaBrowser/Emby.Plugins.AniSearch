using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using MediaBrowser.Model.Drawing;
using System.IO;

namespace Emby.Plugins.AniSearch
{
    public class Plugin : BasePlugin, IHasWebPages, IHasThumbImage
    {
        public ILogger Logger { get; private set; }

        public Plugin(ILogManager logManager)
        {
            Instance = this;
            Logger = logManager.GetLogger(Name);
        }

        public override string Name
        {
            get { return "AniSearch"; }
        }

        public static Plugin Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return Array.Empty<PluginPageInfo>();
        }

        private Guid _id = new Guid("99149DF3-8668-40D8-8AA3-49A40B9B5A9B");

        public override Guid Id
        {
            get { return _id; }
        }

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
        }

        public ImageFormat ThumbImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }
    }
}