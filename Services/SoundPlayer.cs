using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace ToNStatTool.Services
{
    /// <summary>
    /// サウンド再生を管理するクラス
    /// </summary>
    public class SoundPlayer : IDisposable
    {
        private readonly object audioLock = new object();
        private IWavePlayer waveOutDevice;
        private AudioFileReader audioFileReader;
        private readonly ConcurrentQueue<string> soundQueue = new ConcurrentQueue<string>();
        private bool isProcessingQueue = false;
        private bool isDisposed = false;

        /// <summary>
        /// サウンドをキューに追加して再生
        /// </summary>
        public void QueueSound(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"[SOUND] ファイルが存在しません: {filePath}");
                return;
            }

            soundQueue.Enqueue(filePath);
            System.Diagnostics.Debug.WriteLine($"[SOUND] キューに追加: {filePath}, キュー長: {soundQueue.Count}");
            
            ProcessSoundQueue();
        }

        /// <summary>
        /// サウンドキューを処理
        /// </summary>
        private void ProcessSoundQueue()
        {
            if (isProcessingQueue || isDisposed) return;

            Task.Run(() =>
            {
                lock (audioLock)
                {
                    if (isProcessingQueue || isDisposed) return;
                    isProcessingQueue = true;
                }

                try
                {
                    while (soundQueue.TryDequeue(out string filePath) && !isDisposed)
                    {
                        PlaySoundSync(filePath);
                    }
                }
                finally
                {
                    lock (audioLock)
                    {
                        isProcessingQueue = false;
                    }
                }
            });
        }

        /// <summary>
        /// サウンドを同期的に再生
        /// </summary>
        private void PlaySoundSync(string filePath)
        {
            if (isDisposed) return;

            try
            {
                using (var audioFile = new AudioFileReader(filePath))
                using (var outputDevice = new WaveOutEvent())
                {
                    outputDevice.Init(audioFile);
                    outputDevice.Play();
                    
                    while (outputDevice.PlaybackState == PlaybackState.Playing && !isDisposed)
                    {
                        Thread.Sleep(100);
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[SOUND] 再生完了: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SOUND] 再生エラー: {ex.Message}");
                try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
            }
        }

        /// <summary>
        /// カスタムサウンドを再生（デフォルトファイルにフォールバック）
        /// </summary>
        public void PlayCustomSound(string soundPath, string defaultFileName = "warning.mp3")
        {
            try
            {
                string soundFilePath = soundPath;
                
                if (string.IsNullOrEmpty(soundFilePath))
                {
                    soundFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, defaultFileName);
                }

                if (File.Exists(soundFilePath))
                {
                    QueueSound(soundFilePath);
                }
                else
                {
                    System.Media.SystemSounds.Exclamation.Play();
                    System.Diagnostics.Debug.WriteLine("[SOUND] サウンドファイルが見つからないためシステム音を使用");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SOUND] サウンド再生エラー: {ex.Message}");
                System.Media.SystemSounds.Exclamation.Play();
            }
        }

        /// <summary>
        /// MP3ファイルを非同期で再生
        /// </summary>
        public void PlayMp3FileAsync(string filePath)
        {
            lock (audioLock)
            {
                if (isDisposed) return;

                try
                {
                    StopCurrentPlaybackInternal();

                    var newAudioReader = new AudioFileReader(filePath);
                    var newWaveOut = new WaveOutEvent();
                    
                    newWaveOut.Init(newAudioReader);
                    
                    audioFileReader = newAudioReader;
                    waveOutDevice = newWaveOut;

                    waveOutDevice.PlaybackStopped += (sender, e) =>
                    {
                        Task.Run(() => StopCurrentPlayback());
                    };

                    waveOutDevice.Play();
                    System.Diagnostics.Debug.WriteLine($"[SOUND] MP3再生開始: {filePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SOUND] NAudio MP3再生エラー: {ex.Message}");
                    try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
                    StopCurrentPlaybackInternal();
                }
            }
        }

        /// <summary>
        /// 現在の再生を停止
        /// </summary>
        public void StopCurrentPlayback()
        {
            lock (audioLock)
            {
                StopCurrentPlaybackInternal();
            }
        }

        /// <summary>
        /// 現在の再生を停止（内部用、ロックなし）
        /// </summary>
        private void StopCurrentPlaybackInternal()
        {
            var device = waveOutDevice;
            var reader = audioFileReader;
            
            waveOutDevice = null;
            audioFileReader = null;

            if (device != null)
            {
                try
                {
                    device.Stop();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SOUND] デバイス停止エラー: {ex.Message}");
                }

                Thread.Sleep(50);

                try
                {
                    device.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SOUND] デバイス解放エラー（無視）: {ex.Message}");
                }
            }

            if (reader != null)
            {
                try
                {
                    reader.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SOUND] リーダー解放エラー（無視）: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            StopCurrentPlayback();
        }
    }
}
