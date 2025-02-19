/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

#if UNITY_ANDROID && UNITY_EDITOR
#define FAKE_JAR_LOAD
#endif

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Meta.WitAi.Json;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// Class for performing web requests using UnityWebRequest
    /// </summary>
    public class VRequest
    {
        /// <summary>
        /// Will only start new requests if there are less than this number
        /// If <= 0, then all requests will run immediately
        /// </summary>
        public static int MaxConcurrentRequests = 2;
        // Currently transmitting requests
        private static int _requestCount = 0;

        // Request progress delegate
        public delegate void RequestProgressDelegate(float progress);
        // Default request completion delegate
        public delegate void RequestCompleteDelegate<TResult>(TResult result, string error);

        #region INSTANCE
        /// <summary>
        /// Timeout in seconds
        /// </summary>
        public int Timeout { get; set; } = 5;

        /// <summary>
        /// If request is currently being performed
        /// </summary>
        public bool IsPerforming => _performing;
        private bool _performing = false;

        /// <summary>
        /// Current progress for get requests
        /// </summary>
        public float Progress => _progress;
        private float _progress;

        // Actual request
        private UnityWebRequest _request;
        // Callbacks for progress & completion
        private RequestProgressDelegate _onProgress;
        private RequestCompleteDelegate<UnityWebRequest> _onComplete;

        // Coroutine running the request
        private CoroutineUtility.CoroutinePerformer _coroutine;

        // Cancel error
        public const string CANCEL_ERROR = "Cancelled";

        /// <summary>
        /// Initialize with a request and an on completion callback
        /// </summary>
        /// <param name="unityRequest">The unity request to be performed</param>
        /// <param name="onProgress">The callback on get progress</param>
        /// <param name="onComplete">The callback on completion, returns the request & error string</param>
        /// <returns>False if the request cannot be performed</returns>
        public virtual bool Request(UnityWebRequest unityRequest, RequestCompleteDelegate<UnityWebRequest> onComplete, RequestProgressDelegate onProgress = null)
        {
            // Already setup
            if (_request != null)
            {
                onComplete?.Invoke(unityRequest, "Request is already being performed");
                return false;
            }

            // Setup
            _request = unityRequest;
            _onProgress = onProgress;
            _onComplete = onComplete;
            _performing = false;
            _progress = 0f;

            // Add all headers
            Dictionary<string, string> headers = GetHeaders();
            if (headers != null)
            {
                foreach (var key in headers.Keys)
                {
                    _request.SetRequestHeader(key, headers[key]);
                }
            }

            // Use request's timeout value
            _request.timeout = Timeout;

            // Begin
            _coroutine = CoroutineUtility.StartCoroutine(PerformUpdate());

            // Success
            return true;
        }
        /// <summary>
        /// Clean the url prior to use
        /// </summary>
        public virtual string CleanUrl(string url)
        {
            // Get url
            string result = url;
            // Add file:// if needed
            if (!Regex.IsMatch(result, "(http:|https:|file:|jar:).*"))
            {
                result = $"file://{result}";
            }
            // Return url
            return result;
        }
        // Override for custom headers
        protected virtual Dictionary<string, string> GetHeaders() => null;
        // Perform update
        protected virtual IEnumerator PerformUpdate()
        {
            // Continue while request exists
            while (_request != null && !_request.isDone)
            {
                // Wait
                yield return null;

                // Waiting to begin
                if (!_performing)
                {
                    // Can start
                    if (MaxConcurrentRequests <= 0 || _requestCount < MaxConcurrentRequests)
                    {
                        _requestCount++;
                        Begin();
                    }
                }
                // Update progress
                else
                {
                    float newProgress = Mathf.Max(_request.downloadProgress, _request.uploadProgress);
                    if (_progress != newProgress)
                    {
                        _progress = newProgress;
                        _onProgress?.Invoke(_progress);
                    }
                }
            }
            // Complete
            Complete();
        }
        // Begin request
        protected virtual void Begin()
        {
            _performing = true;
            _progress = 0f;
            _onProgress?.Invoke(_progress);
            _request.SendWebRequest();
        }
        // Request complete
        protected virtual void Complete()
        {
            // Perform callback
            if (_performing && _request != null && _request.isDone)
            {
                _progress = 1f;
                _onProgress?.Invoke(_progress);
                _onComplete?.Invoke(_request, _request.error);
            }

            // Unload
            Unload();
        }
        // Abort request
        public virtual void Cancel()
        {
            // Cancel
            if (_onComplete != null && _request != null)
            {
                _progress = 1f;
                _onProgress?.Invoke(_progress);
                _onComplete?.Invoke(_request, CANCEL_ERROR);
            }

            // Unload
            Unload();
        }
        // Request destroy
        protected virtual void Unload()
        {
            // Cancel coroutine
            if (_coroutine != null)
            {
                _coroutine.CoroutineCancel();
                _coroutine = null;
            }

            // Complete
            if (_performing)
            {
                _performing = false;
                _requestCount--;
            }

            // Remove delegates
            _onProgress = null;
            _onComplete = null;

            // Dispose
            if (_request != null)
            {
                _request.Dispose();
                _request = null;
            }
        }
        #endregion

        #region FILE
        /// <summary>
        /// Performs a simple http header request
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="onComplete">Called once header lookup has completed</param>
        /// <returns></returns>
        public bool RequestFileHeaders(Uri uri,
            RequestCompleteDelegate<Dictionary<string, string>> onComplete)
        {
            // Header unity request
            UnityWebRequest unityRequest = UnityWebRequest.Head(uri);

            // Perform request
            return Request(unityRequest, (response, error) =>
            {
                // Error
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(null, error);
                    return;
                }

                // Headers dictionary if possible
                Dictionary<string, string> headers = response.GetResponseHeaders();
                if (headers == null)
                {
                    onComplete?.Invoke(null, "No headers in response.");
                    return;
                }

                // Success
                onComplete?.Invoke(headers, string.Empty);
            });
        }

        /// <summary>
        /// Performs a simple http header request
        /// </summary>
        /// <param name="uri">Uri to get a file</param>
        /// <param name="onComplete">Called once file data has been loaded</param>
        /// <returns>False if cannot begin request</returns>
        public bool RequestFile(Uri uri,
            RequestCompleteDelegate<byte[]> onComplete,
            RequestProgressDelegate onProgress = null)
        {
            // Get unity request
            UnityWebRequest unityRequest = UnityWebRequest.Get(uri);
            // Perform request
            return Request(unityRequest, (response, error) =>
            {
                // Error
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(null, error);
                    return;
                }

                // File data
                byte[] fileData = response?.downloadHandler?.data;
                if (fileData == null)
                {
                    onComplete?.Invoke(null, "No data in response");
                    return;
                }

                // Success
                onComplete?.Invoke(fileData, string.Empty);
            }, onProgress);
        }

        /// <summary>
        /// Download a file using a unityrequest
        /// </summary>
        /// <param name="unityRequest">The unity request to add a download handler to</param>
        /// <param name="onComplete">Called once download has completed</param>
        /// <param name="onProgress">Download progress delegate</param>
        public bool RequestFileDownload(string downloadPath, UnityWebRequest unityRequest,
            RequestCompleteDelegate<bool> onComplete,
            RequestProgressDelegate onProgress = null)
        {
            // Get temporary path for download
            string tempDownloadPath = downloadPath + ".tmp";
            try
            {
                // Delete temporary file if it already exists
                if (File.Exists(tempDownloadPath))
                {
                    File.Delete(tempDownloadPath);
                }
            }
            catch (Exception e)
            {
                // Failed to delete file
                VLog.W($"Deleting Download File Failed\nPath: {tempDownloadPath}\n\n{e}");
            }

            // Add file download handler
            DownloadHandlerFile fileHandler = new DownloadHandlerFile(tempDownloadPath, true);
            unityRequest.downloadHandler = fileHandler;
            unityRequest.disposeDownloadHandlerOnDispose = true;

            // Perform request
            return Request(unityRequest, (response, error) =>
            {
                try
                {
                    // Handle existing temp file
                    if (File.Exists(tempDownloadPath))
                    {
                        // For error, remove
                        if (!string.IsNullOrEmpty(error))
                        {
                            File.Delete(tempDownloadPath);
                        }
                        // For success, move to final path
                        else
                        {
                            // File already at download path, delete it
                            if (File.Exists(downloadPath))
                            {
                                File.Delete(downloadPath);
                            }

                            // Move to final path
                            File.Move(tempDownloadPath, downloadPath);
                        }
                    }
                }
                catch (Exception e)
                {
                    VLog.W($"Moving Download File Failed\nFrom: {tempDownloadPath}\nTo: {downloadPath}\n\n{e}");
                }

                // Complete
                onComplete?.Invoke(string.IsNullOrEmpty(error), error);
            }, onProgress);
        }

        /// <summary>
        /// Checks if a file exists at a specified location
        /// </summary>
        /// <param name="checkPath">The local file path to be checked</param>
        /// <param name="onComplete">Called once check has completed.  Returns true if file exists</param>
        public bool RequestFileExists(string checkPath,
            RequestCompleteDelegate<bool> onComplete)
        {
            // WebGL & web files, perform a header lookup
            if (checkPath.StartsWith("http"))
            {
                Uri uri = new Uri(CleanUrl(checkPath));
                return RequestFileHeaders(uri, (headers, error) => onComplete?.Invoke(headers != null, error));
            }

#if FAKE_JAR_LOAD
            // Android editor: simulate jar handling
            if (Application.isPlaying && checkPath.StartsWith(Application.streamingAssetsPath))
#else
            // Jar file (Android streaming assets)
            if (checkPath.StartsWith("jar"))
#endif
            {
                Uri uri = new Uri(CleanUrl(checkPath));
                return RequestFile(uri, (response, error) =>
                {
                    // If getting here, most likely failed but double check anyway
                    onComplete?.Invoke(string.IsNullOrEmpty(error), error);
                },
                (progress) =>
                {
                    // Stop as early as possible
                    if (progress > 0f && progress < 1f)
                    {
                        var localHandle = onComplete;
                        onComplete = null;
                        Cancel();
                        localHandle?.Invoke(true, String.Empty);
                        VLog.D("Async Check File Exists Success");
                    }
                });
            }

            // Can simply use File.IO otherwise
            bool found = File.Exists(checkPath);
            onComplete?.Invoke(found, string.Empty);
            return true;
        }
        #endregion

        #region TEXT
        /// <summary>
        /// Performs a text request & handles the resultant text
        /// </summary>
        /// <param name="unityRequest">The unity request performing the post or get</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <param name="onProgress">The text download progress</param>
        public bool RequestText(UnityWebRequest unityRequest,
            RequestCompleteDelegate<string> onComplete,
            RequestProgressDelegate onProgress = null)
        {
            return Request(unityRequest, (response, error) =>
            {
                // Request error
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(string.Empty, error);
                    return;
                }
                // No text returned
                string text = response.downloadHandler.text;
                if (string.IsNullOrEmpty(text))
                {
                    onComplete?.Invoke(string.Empty, "No response contents found");
                    return;
                }
                // Success
                onComplete?.Invoke(text, string.Empty);
            }, onProgress);
        }
        #endregion

        #region JSON
        /// <summary>
        /// Performs a json request & handles the resultant text
        /// </summary>
        /// <param name="unityRequest">The unity request performing the post or get</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <param name="onProgress">The text download progress</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJson<TData>(UnityWebRequest unityRequest,
            RequestCompleteDelegate<TData> onComplete,
            RequestProgressDelegate onProgress = null)
        {
            // Set request header for json
            unityRequest.SetRequestHeader("Content-Type", "application/json");

            // Perform text request
            return RequestText(unityRequest, (text, error) =>
            {
                // Request error
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(default(TData), error);
                    return;
                }

                // Deserialize
                JsonConvert.DeserializeObjectAsync<TData>(text, (result, deserializeSuccess) =>
                {
                    // Failed
                    if (!deserializeSuccess)
                    {
                        onComplete?.Invoke(result, $"Failed to parse json\n{text}");
                    }
                    // Success
                    else
                    {
                        onComplete?.Invoke(result, string.Empty);
                    }
                });
            }, onProgress);
        }

        /// <summary>
        /// Perform a json get request with a specified uri
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <param name="onProgress">The text download progress</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        /// <returns></returns>
        public bool RequestJson<TData>(Uri uri,
            RequestCompleteDelegate<TData> onComplete,
            RequestProgressDelegate onProgress = null)
        {
            return RequestJson(UnityWebRequest.Get(uri), onComplete, onProgress);
        }

        /// <summary>
        /// Performs a json request by posting byte data
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="postData">The data to be uploaded</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <param name="onProgress">The data upload progress</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJson<TData>(Uri uri, byte[] postData,
            RequestCompleteDelegate<TData> onComplete,
            RequestProgressDelegate onProgress = null)
        {
            var unityRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST);
            unityRequest.uploadHandler = new UploadHandlerRaw(postData);
            unityRequest.disposeUploadHandlerOnDispose = true;
            unityRequest.downloadHandler = new DownloadHandlerBuffer();
            unityRequest.disposeUploadHandlerOnDispose = true;
            return RequestJson(unityRequest, onComplete, onProgress);
        }

        /// <summary>
        /// Performs a json request by posting byte data
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="postText">The string to be uploaded</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <param name="onProgress">The data upload progress</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJson<TData>(Uri uri, string postText,
            RequestCompleteDelegate<TData> onComplete,
            RequestProgressDelegate onProgress = null)
        {
            return RequestJson(uri, Encoding.UTF8.GetBytes(postText), onComplete, onProgress);
        }
        #endregion

        #region AUDIO CLIPS
        /// <summary>
        /// Request audio clip with url, type, progress delegate & ready delegate
        /// </summary>
        /// <param name="unityRequest">The unity request to add a download handler to</param>
        /// <param name="onClipReady">Called when the clip is ready for playback or has failed to load</param>
        /// <param name="audioType">The audio type requested (Wav, MP3, etc.)</param>
        /// <param name="audioStream">Whether or not audio should be streamed</param>
        /// <param name="onProgress">Clip progress callback</param>
        public bool RequestAudioClip(UnityWebRequest unityRequest,
            RequestCompleteDelegate<AudioClip> onClipReady,
            AudioType audioType = AudioType.UNKNOWN, bool audioStream = true,
            RequestProgressDelegate onProgress = null)
        {
            // Attempt to determine audio type if set to unknown
            if (audioType == AudioType.UNKNOWN)
            {
                // Determine audio type from extension
                string audioExt = Path.GetExtension(unityRequest.uri.ToString()).Replace(".", "");
                if (!Enum.TryParse(audioExt, true, out audioType))
                {
                    onClipReady?.Invoke(null, $"Unknown audio type\nUrl: {unityRequest.uri}");
                    return false;
                }
            }

            // Add audio download handler
            if (unityRequest.downloadHandler == null)
            {
                unityRequest.downloadHandler = new DownloadHandlerAudioClip(unityRequest.uri, audioType);
            }

            // Set stream settings
            var audioDownloader = unityRequest.downloadHandler as DownloadHandlerAudioClip;
            if (audioDownloader != null)
            {
                audioDownloader.streamAudio = audioStream;
            }

            // Perform default request operation
            return Request(unityRequest,
                (response, error) =>
                {
                    // Request error
                    if (!string.IsNullOrEmpty(error))
                    {
                        onClipReady?.Invoke(null, error);
                        return;
                    }

                    // Get clip
                    AudioClip clip = null;
                    try
                    {
                        clip = DownloadHandlerAudioClip.GetContent(response);
                    }
                    catch (Exception exception)
                    {
                        // Failed to decode audio clip
                        onClipReady?.Invoke(null, $"Failed to decode audio clip\n{exception.ToString()}");
                        return;
                    }

                    // Clip is still missing
                    if (clip == null)
                    {
                        onClipReady?.Invoke(null, "Failed to decode audio clip");
                        return;
                    }

                    // Set clip name to audio url name
                    string newName = Path.GetFileNameWithoutExtension(unityRequest.uri.ToString());
                    if (!string.IsNullOrEmpty(newName))
                    {
                        clip.name = newName;
                    }

                    // Return clip
                    onClipReady?.Invoke(clip, string.Empty);
                }, onProgress);
        }

        /// <summary>
        /// Request audio clip with url, type, progress delegate & ready delegate
        /// </summary>
        /// <param name="uri">The url to be called</param>
        /// <param name="onClipReady">Called when the clip is ready for playback or has failed to load</param>
        /// <param name="audioType">The audio type requested (Wav, MP3, etc.)</param>
        /// <param name="audioStream">Whether or not audio should be streamed</param>
        /// <param name="onProgress">Clip progress callback</param>
        public bool RequestAudioClip(Uri uri,
            RequestCompleteDelegate<AudioClip> onClipReady,
            AudioType audioType = AudioType.UNKNOWN, bool audioStream = true,
            RequestProgressDelegate onProgress = null)
        {
            UnityWebRequest unityRequest = UnityWebRequestMultimedia.GetAudioClip(uri, audioType);
            return RequestAudioClip(unityRequest, onClipReady, audioType, audioStream, onProgress);
        }
        #endregion
    }
}
