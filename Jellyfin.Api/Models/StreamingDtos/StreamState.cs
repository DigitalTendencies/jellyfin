using System;
using Jellyfin.Api.Helpers;
using Jellyfin.Api.Models.PlaybackDtos;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;

namespace Jellyfin.Api.Models.StreamingDtos
{
    /// <summary>
    /// The stream state dto.
    /// </summary>
    public class StreamState : EncodingJobInfo, IDisposable
    {
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly TranscodingJobHelper _transcodingJobHelper;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamState" /> class.
        /// </summary>
        /// <param name="mediaSourceManager">Instance of the <see cref="IMediaSourceManager" /> interface.</param>
        /// <param name="transcodingType">The <see cref="TranscodingJobType" />.</param>
        /// <param name="transcodingJobHelper">The <see cref="TranscodingJobHelper" /> singleton.</param>
        public StreamState(IMediaSourceManager mediaSourceManager, TranscodingJobType transcodingType, TranscodingJobHelper transcodingJobHelper)
            : base(transcodingType)
        {
            _mediaSourceManager = mediaSourceManager;
            _transcodingJobHelper = transcodingJobHelper;
        }

        /// <summary>
        /// Gets or sets the requested url.
        /// </summary>
        public string? RequestedUrl { get; set; }

        /// <summary>
        /// Gets or sets the request.
        /// </summary>
        public StreamingRequestDto Request
        {
            get => (StreamingRequestDto)BaseRequest;
            set
            {
                BaseRequest = value;
                IsVideoRequest = VideoRequest != null;
            }
        }

        /// <summary>
        /// Gets or sets the transcoding throttler.
        /// </summary>
        public TranscodingThrottler? TranscodingThrottler { get; set; }

        /// <summary>
        /// Gets the video request.
        /// </summary>
        public VideoRequestDto? VideoRequest => Request! as VideoRequestDto;

        /// <summary>
        /// Gets or sets the direct stream provicer.
        /// </summary>
        /// <remarks>
        /// Deprecated.
        /// </remarks>
        public IDirectStreamProvider? DirectStreamProvider { get; set; }

        /// <summary>
        /// Gets or sets the path to wait for.
        /// </summary>
        public string? WaitForPath { get; set; }

        /// <summary>
        /// Gets a value indicating whether the request outputs video.
        /// </summary>
        public bool IsOutputVideo => Request is VideoRequestDto;

        /// <summary>
        /// Gets the segment length.
        /// </summary>
        public int SegmentLength
        {
            get
            {
                if (Request.SegmentLength.HasValue)
                {
                    return Request.SegmentLength.Value;
                }

                if (EncodingHelper.IsCopyCodec(OutputVideoCodec))
                {
                    var userAgent = UserAgent ?? string.Empty;

                    if (userAgent.IndexOf("AppleTV", StringComparison.OrdinalIgnoreCase) != -1
                        || userAgent.IndexOf("cfnetwork", StringComparison.OrdinalIgnoreCase) != -1
                        || userAgent.IndexOf("ipad", StringComparison.OrdinalIgnoreCase) != -1
                        || userAgent.IndexOf("iphone", StringComparison.OrdinalIgnoreCase) != -1
                        || userAgent.IndexOf("ipod", StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        return 6;
                    }

                    if (IsSegmentedLiveStream)
                    {
                        return 3;
                    }

                    return 6;
                }

                return 3;
            }
        }

        /// <summary>
        /// Gets the minimum number of segments.
        /// </summary>
        public int MinSegments
        {
            get
            {
                if (Request.MinSegments.HasValue)
                {
                    return Request.MinSegments.Value;
                }

                return SegmentLength >= 10 ? 2 : 3;
            }
        }

        /// <summary>
        /// Gets or sets the user agent.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to estimate the content length.
        /// </summary>
        public bool EstimateContentLength { get; set; }

        /// <summary>
        /// Gets or sets the transcode seek info.
        /// </summary>
        public TranscodeSeekInfo TranscodeSeekInfo { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable dlna headers.
        /// </summary>
        public bool EnableDlnaHeaders { get; set; }

        /// <summary>
        /// Gets or sets the device profile.
        /// </summary>
        public DeviceProfile? DeviceProfile { get; set; }

        /// <summary>
        /// Gets or sets the transcoding job.
        /// </summary>
        public TranscodingJobDto? TranscodingJob { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public override void ReportTranscodingProgress(TimeSpan? transcodingPosition, float? framerate, double? percentComplete, long? bytesTranscoded, int? bitRate)
        {
            _transcodingJobHelper.ReportTranscodingProgress(TranscodingJob!, this, transcodingPosition, framerate, percentComplete, bytesTranscoded, bitRate);
        }

        /// <summary>
        /// Disposes the stream state.
        /// </summary>
        /// <param name="disposing">Whether the object is currently beeing disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // REVIEW: Is this the right place for this?
                if (MediaSource.RequiresClosing
                    && string.IsNullOrWhiteSpace(Request.LiveStreamId)
                    && !string.IsNullOrWhiteSpace(MediaSource.LiveStreamId))
                {
                    _mediaSourceManager.CloseLiveStream(MediaSource.LiveStreamId).GetAwaiter().GetResult();
                }

                TranscodingThrottler?.Dispose();
            }

            TranscodingThrottler = null;
            TranscodingJob = null;

            _disposed = true;
        }
    }
}
