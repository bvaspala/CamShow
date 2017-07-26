using AForge.Video.DirectShow;

namespace CamShow
{
    class VideoCapabilitiesDisplayable
    {
        public VideoCapabilities Capability { get; set; }

        public VideoCapabilitiesDisplayable(VideoCapabilities capability)
        {
            this.Capability = capability;
        }

        public double AspectRatio { get { return (double) Capability.FrameSize.Width / Capability.FrameSize.Height; } }

        public override string ToString()
        {
            return Capability.FrameSize.Width.ToString() + " x " + Capability.FrameSize.Height.ToString();
        }
    }
}
