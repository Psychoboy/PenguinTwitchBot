using Microsoft.Extensions.Logging.Abstractions;
using PenguinTwitchBot.Services;
using System.Text;
using Xunit;

namespace PenguinTwitchBot.Test
{
    public class MediaUploadServiceTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly MediaUploadService _service;

        public MediaUploadServiceTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "MediaUploadServiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRoot);
            _service = new MediaUploadService(_testRoot, NullLogger<MediaUploadService>.Instance);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
            {
                try { Directory.Delete(_testRoot, true); } catch { /* Ignore cleanup issues in temp */ }
            }
        }

        private static byte[] CreateFakeMp3()
        {
            var bytes = new byte[64];
            bytes[0] = 0x49; // 'I'
            bytes[1] = 0x44; // 'D'
            bytes[2] = 0x33; // '3'
            return bytes;
        }

        private static byte[] CreateFakeWav()
        {
            var bytes = new byte[64];
            bytes[0] = (byte)'R'; bytes[1] = (byte)'I'; bytes[2] = (byte)'F'; bytes[3] = (byte)'F';
            bytes[8] = (byte)'W'; bytes[9] = (byte)'A'; bytes[10] = (byte)'V'; bytes[11] = (byte)'E';
            return bytes;
        }

        private static byte[] CreateFakeOgg()
        {
            var bytes = new byte[64];
            bytes[0] = (byte)'O'; bytes[1] = (byte)'g'; bytes[2] = (byte)'g'; bytes[3] = (byte)'S';
            return bytes;
        }

        private static byte[] CreateFakeGif()
        {
            return Encoding.ASCII.GetBytes("GIF89a" + new string('\0', 30));
        }

        private static byte[] CreateFakePng()
        {
            var bytes = new byte[64];
            bytes[0] = 0x89; bytes[1] = 0x50; bytes[2] = 0x4E; bytes[3] = 0x47;
            bytes[4] = 0x0D; bytes[5] = 0x0A; bytes[6] = 0x1A; bytes[7] = 0x0A;
            return bytes;
        }

        private static byte[] CreateFakeMp4()
        {
            var bytes = new byte[64];
            bytes[4] = (byte)'f'; bytes[5] = (byte)'t'; bytes[6] = (byte)'y'; bytes[7] = (byte)'p';
            return bytes;
        }

        private static byte[] CreateFakeWebm()
        {
            var bytes = new byte[64];
            bytes[0] = 0x1A; bytes[1] = 0x45; bytes[2] = 0xDF; bytes[3] = 0xA3;
            return bytes;
        }

        [Fact]
        public void ValidateAudioSignature_ValidFormats_ReturnsTrue()
        {
            Assert.True(MediaUploadService.ValidateAudioSignature(CreateFakeMp3(), ".mp3"));
            Assert.True(MediaUploadService.ValidateAudioSignature(CreateFakeWav(), ".wav"));
            Assert.True(MediaUploadService.ValidateAudioSignature(CreateFakeOgg(), ".ogg"));
        }

        [Fact]
        public void ValidateAudioSignature_InvalidContent_ReturnsFalse()
        {
            var badData = Encoding.UTF8.GetBytes("This is plain text not audio");
            Assert.False(MediaUploadService.ValidateAudioSignature(badData, ".mp3"));
            Assert.False(MediaUploadService.ValidateAudioSignature(badData, ".wav"));
            Assert.False(MediaUploadService.ValidateAudioSignature(badData, ".ogg"));
        }

        [Fact]
        public void ValidateImageSignature_ValidAndInvalidFormats()
        {
            Assert.True(MediaUploadService.ValidateImageSignature(CreateFakeGif(), ".gif"));
            Assert.True(MediaUploadService.ValidateImageSignature(CreateFakePng(), ".png"));
            Assert.False(MediaUploadService.ValidateImageSignature(Encoding.UTF8.GetBytes("Not an image"), ".gif"));
        }

        [Fact]
        public void ValidateVideoSignature_ValidFormats_ReturnsTrue()
        {
            Assert.True(MediaUploadService.ValidateVideoSignature(CreateFakeMp4(), ".mp4"));
            Assert.True(MediaUploadService.ValidateVideoSignature(CreateFakeOgg(), ".ogv"));
        }

        [Fact]
        public void SanitizeFileName_StripsInvalidCharsAndPathTraversal()
        {
            var input = "../../evil/path/file*name?.mp3";
            var sanitized = MediaUploadService.SanitizeFileName(input);
            Assert.DoesNotContain("/", sanitized);
            Assert.DoesNotContain("\\", sanitized);
            Assert.DoesNotContain("..", sanitized);
            Assert.DoesNotContain("*", sanitized);
            Assert.DoesNotContain("?", sanitized);
        }

        [Fact]
        public async Task UploadAudioAsync_ValidAudio_SavesAndResolvesUrl()
        {
            var mp3Bytes = CreateFakeMp3();
            using var stream = new MemoryStream(mp3Bytes);

            var result = await _service.UploadAudioAsync(stream, "my_custom_sound.mp3");

            Assert.True(result.Success);
            Assert.Equal("my_custom_sound", result.BaseName);
            Assert.Equal("my_custom_sound.mp3", result.FileName);

            var audioFiles = _service.GetAudioFiles();
            Assert.Contains("my_custom_sound", audioFiles);

            var url = _service.ResolveAudioUrl("my_custom_sound");
            Assert.Equal("/audio/my_custom_sound.mp3", url);
        }

        [Fact]
        public async Task UploadAlertMediaAsync_ValidMedia_SavesAndLists()
        {
            var gifBytes = CreateFakeGif();
            using var stream = new MemoryStream(gifBytes);

            var result = await _service.UploadAlertMediaAsync(stream, "stream_hype.gif");

            Assert.True(result.Success);
            Assert.Equal("stream_hype.gif", result.FileName);

            var mediaFiles = _service.GetAlertMediaFiles();
            Assert.Contains("stream_hype.gif", mediaFiles);

            var url = _service.ResolveAlertMediaUrl("stream_hype.gif");
            Assert.Equal("/gifs/stream_hype.gif", url);
        }

        [Fact]
        public async Task UploadAlertCompanionAudioAsync_EnforcesBaseNameMatching()
        {
            // 1. Upload base alert media
            var gifBytes = CreateFakeGif();
            using (var stream = new MemoryStream(gifBytes))
            {
                var mediaResult = await _service.UploadAlertMediaAsync(stream, "celebration.gif");
                Assert.True(mediaResult.Success);
            }

            // 2. Upload companion sound with different original filename
            var mp3Bytes = CreateFakeMp3();
            using (var stream = new MemoryStream(mp3Bytes))
            {
                var soundResult = await _service.UploadAlertCompanionAudioAsync(stream, "celebration", "any_random_name.mp3");
                Assert.True(soundResult.Success);
                // Must be saved matching the media base name!
                Assert.Equal("celebration.mp3", soundResult.FileName);
            }

            // 3. Verify companion audio is discovered
            var companion = _service.GetCompanionAudioFileName("celebration.gif");
            Assert.Equal("celebration.mp3", companion);

            var soundUrl = _service.ResolveAlertSoundUrl(companion);
            Assert.Equal("/gifs/celebration.mp3", soundUrl);
        }

        [Fact]
        public async Task UploadAlertCompanionAudioAsync_ReplacesOldFormat()
        {
            // First companion sound as .wav
            var wavBytes = CreateFakeWav();
            using (var stream = new MemoryStream(wavBytes))
            {
                await _service.UploadAlertCompanionAudioAsync(stream, "dance", "sound.wav");
            }

            var firstCompanion = _service.GetCompanionAudioFileName("dance.gif");
            Assert.Equal("dance.wav", firstCompanion);

            // Replace with .mp3
            var mp3Bytes = CreateFakeMp3();
            using (var stream = new MemoryStream(mp3Bytes))
            {
                await _service.UploadAlertCompanionAudioAsync(stream, "dance", "sound.mp3");
            }

            // The .wav should be replaced by .mp3 to avoid collision
            var updatedCompanion = _service.GetCompanionAudioFileName("dance.gif");
            Assert.Equal("dance.mp3", updatedCompanion);

            var oldWavPath = Path.Combine(_service.GifsDirectory, "dance.wav");
            Assert.False(File.Exists(oldWavPath));
        }

        [Fact]
        public async Task UploadAudioAsync_ChoosesUnusedNumericSuffixOnCollision()
        {
            var mp3Bytes = CreateFakeMp3();

            // First upload
            using (var stream = new MemoryStream(mp3Bytes))
            {
                var result1 = await _service.UploadAudioAsync(stream, "fanfare.mp3");
                Assert.True(result1.Success);
                Assert.Equal("fanfare", result1.BaseName);
                Assert.Equal("fanfare.mp3", result1.FileName);
            }

            // Second upload with same name
            using (var stream = new MemoryStream(mp3Bytes))
            {
                var result2 = await _service.UploadAudioAsync(stream, "fanfare.mp3");
                Assert.True(result2.Success);
                Assert.Equal("fanfare_1", result2.BaseName);
                Assert.Equal("fanfare_1.mp3", result2.FileName);
            }

            // Third upload with same name
            using (var stream = new MemoryStream(mp3Bytes))
            {
                var result3 = await _service.UploadAudioAsync(stream, "fanfare.mp3");
                Assert.True(result3.Success);
                Assert.Equal("fanfare_2", result3.BaseName);
                Assert.Equal("fanfare_2.mp3", result3.FileName);
            }
        }

        [Fact]
        public async Task UploadAlertCompanionAudioAsync_RejectsSameExtensionAsMedia()
        {
            var webmBytes = CreateFakeWebm();

            // 1. Upload video media
            using (var stream = new MemoryStream(webmBytes))
            {
                var mediaResult = await _service.UploadAlertMediaAsync(stream, "clip.webm");
                Assert.True(mediaResult.Success);
            }

            // 2. Upload companion audio with same extension (.webm) -> should be rejected!
            using (var stream = new MemoryStream(webmBytes))
            {
                var soundResult = await _service.UploadAlertCompanionAudioAsync(stream, "clip.webm", "sound.webm");
                Assert.False(soundResult.Success);
                Assert.Contains("cannot be the same", soundResult.ErrorMessage);
            }
        }

        [Fact]
        public async Task UploadAlertCompanionAudioAsync_DoesNotDeleteVideoMediaFile()
        {
            var webmBytes = CreateFakeWebm();
            var mp3Bytes = CreateFakeMp3();

            // 1. Upload webm video media
            using (var stream = new MemoryStream(webmBytes))
            {
                var mediaResult = await _service.UploadAlertMediaAsync(stream, "action.webm");
                Assert.True(mediaResult.Success);
            }

            var videoPath = Path.Combine(_service.GifsDirectory, "action.webm");
            Assert.True(File.Exists(videoPath));

            // 2. Upload companion audio .mp3
            using (var stream = new MemoryStream(mp3Bytes))
            {
                var soundResult = await _service.UploadAlertCompanionAudioAsync(stream, "action.webm", "action.mp3");
                Assert.True(soundResult.Success);
            }

            // 3. Verify video media file was NOT deleted during companion audio cleanup
            Assert.True(File.Exists(videoPath), "Video media file was deleted during companion audio cleanup!");

            // 4. Verify companion lookup returns the audio, not the video itself
            var companion = _service.GetCompanionAudioFileName("action.webm");
            Assert.Equal("action.mp3", companion);
        }

        [Fact]
        public async Task GetAlertMediaFiles_DoesNotListCompanionAudio()
        {
            var gifBytes = CreateFakeGif();
            var mp3Bytes = CreateFakeMp3();

            // Upload alert media
            using (var stream = new MemoryStream(gifBytes))
            {
                await _service.UploadAlertMediaAsync(stream, "cheer.gif");
            }

            // Upload companion audio
            using (var stream = new MemoryStream(mp3Bytes))
            {
                await _service.UploadAlertCompanionAudioAsync(stream, "cheer.gif", "audio.mp3");
            }

            var alertMedia = _service.GetAlertMediaFiles();
            Assert.Contains("cheer.gif", alertMedia);
            Assert.DoesNotContain("cheer.mp3", alertMedia);
        }
    }
}
