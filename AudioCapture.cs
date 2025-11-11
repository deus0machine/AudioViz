using System;
using NAudio.Wave;
using NAudio.Dsp;
using System.Collections.Generic;

namespace AudioViz
{
    public class AudioCapture : IDisposable
    {
        private WasapiLoopbackCapture? _capture;
        private readonly float[] _audioBuffer;
        private readonly float[] _fftBuffer;
        private readonly Complex[] _fftComplex;
        private readonly object _bufferLock = new object();
        private readonly int _bufferSize;
        private readonly int _fftSize;

        public AudioCapture(int bufferSize = 4096, int fftSize = 2048)
        {
            _bufferSize = bufferSize;
            _fftSize = fftSize;
            _audioBuffer = new float[bufferSize];
            _fftBuffer = new float[fftSize];
            _fftComplex = new Complex[fftSize];
        }

        public void Start()
        {
            try
            {
                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnDataAvailable;
                _capture.StartRecording();
                Console.WriteLine("Audio capture started successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio capture failed: {ex.Message}");
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            var buffer = new float[e.BytesRecorded / 4];
            System.Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

            lock (_bufferLock)
            {
                Array.Copy(buffer, _audioBuffer, Math.Min(buffer.Length, _audioBuffer.Length));

                // Применяем оконную функцию Ханна и готовим данные для FFT
                int length = Math.Min(buffer.Length, _fftComplex.Length);
                for (int i = 0; i < length; i++)
                {
                    float window = 0.5f * (1 - (float)Math.Cos(2 * Math.PI * i / (length - 1)));
                    _fftComplex[i].X = buffer[i] * window;
                    _fftComplex[i].Y = 0;
                }

                // Выполняем FFT
                FastFourierTransform.FFT(true, (int)Math.Log(_fftComplex.Length, 2), _fftComplex);

                // Вычисляем magnitudes с улучшенной обработкой для лучшей видимости
                for (int i = 0; i < _fftBuffer.Length; i++)
                {
                    float magnitude = (float)Math.Sqrt(_fftComplex[i].X * _fftComplex[i].X + 
                                                       _fftComplex[i].Y * _fftComplex[i].Y);
                    // Увеличенная чувствительность и яркость
                    _fftBuffer[i] = (float)Math.Log(1 + magnitude * 500) * 0.8f;
                }
            }
        }

        public void GetAudioData(float[] output)
        {
            lock (_bufferLock)
            {
                Array.Copy(_audioBuffer, output, Math.Min(_audioBuffer.Length, output.Length));
            }
        }

        public void GetFFTData(float[] output)
        {
            lock (_bufferLock)
            {
                Array.Copy(_fftBuffer, output, Math.Min(_fftBuffer.Length, output.Length));
            }
        }

        public float GetRMS()
        {
            lock (_bufferLock)
            {
                float sum = 0;
                for (int i = 0; i < _audioBuffer.Length; i++)
                {
                    sum += _audioBuffer[i] * _audioBuffer[i];
                }
                return (float)Math.Sqrt(sum / _audioBuffer.Length);
            }
        }

        public float GetFrequencyBand(int startIndex, int endIndex)
        {
            lock (_bufferLock)
            {
                float sum = 0;
                int count = 0;
                for (int i = startIndex; i < Math.Min(endIndex, _fftBuffer.Length); i++)
                {
                    sum += _fftBuffer[i];
                    count++;
                }
                return count > 0 ? sum / count : 0;
            }
        }

        public void Dispose()
        {
            _capture?.StopRecording();
            _capture?.Dispose();
        }
    }
}

