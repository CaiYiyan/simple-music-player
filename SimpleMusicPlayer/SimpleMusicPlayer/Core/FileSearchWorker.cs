﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using SimpleMusicPlayer.Core.Interfaces;

namespace SimpleMusicPlayer.Core
{
    public class FileSearchWorker : ReactiveObject
    {
        private readonly string[] extensions = new[] { ".mp3", ".wma", ".ogg", ".wav" };
        // action for media file creation
        private readonly Func<string, IMediaFile> createMediaFileFunc;

        public FileSearchWorker(Func<string, IMediaFile> createMediaFileFunc)
        {
            this.createMediaFileFunc = createMediaFileFunc;

            this.canStartSearch = this.WhenAny(x => x.MainTask, x => x.MainTask.IsCompleted,
                                               (task, iscompleted) => task.Value == null || iscompleted.Value)
                                      .ToProperty(this, x => x.CanStartSearch);

            this.isBusy = this.WhenAny(x => x.MainTask, x => x.MainTask.IsCompleted,
                                       (task, iscompleted) => task.Value != null && !iscompleted.Value)
                              .ToProperty(this, x => x.IsBusy);

            this.StopSearchCmd = ReactiveCommand.Create(this.WhenAny(x => x.IsBusy, x => x.CancelToken,
                                                                     (isbusy, canceltoken) => isbusy.Value && canceltoken.Value != null));
            this.StopSearchCmd.Subscribe(_ => this.CancelToken.Cancel());
        }

        private Task<IEnumerable<IMediaFile>> mainTask;
        public Task<IEnumerable<IMediaFile>> MainTask
        {
            get { return this.mainTask; }
            private set { this.RaiseAndSetIfChanged(ref mainTask, value); }
        }

        private bool isWorking;
        public bool IsWorking
        {
            get { return this.isWorking; }
            private set { this.RaiseAndSetIfChanged(ref isWorking, value); }
        }

        private CancellationTokenSource cancelToken;
        public CancellationTokenSource CancelToken
        {
            get { return this.cancelToken; }
            private set { this.RaiseAndSetIfChanged(ref cancelToken, value); }
        }

        private ObservableAsPropertyHelper<bool> isBusy;
        public bool IsBusy
        {
            get { return isBusy.Value; }
        }

        private ObservableAsPropertyHelper<bool> canStartSearch;
        public bool CanStartSearch
        {
            get { return canStartSearch.Value; }
        }

        public ReactiveCommand<object> StopSearchCmd { get; private set; }

        public async Task<IEnumerable<IMediaFile>> StartSearchAsync(IList filesOrDirsCollection)
        {
            this.IsWorking = true;
            // create the cancellation token source
            this.CancelToken = new CancellationTokenSource();
            // create the cancellation token
            var token = this.CancelToken.Token;

            this.MainTask = Task<IEnumerable<IMediaFile>>.Factory
              .StartNew(() => {
                  var results = new ConcurrentQueue<IMediaFile>();

                  // get audio files from input collection
                  var rawFiles = filesOrDirsCollection.OfType<string>().Where(this.IsAudioFile).OrderBy(s => s).ToList();
                  foreach (var rawFile in rawFiles.TakeWhile(rawDir => !token.IsCancellationRequested))
                  {
                      var mf = this.GetMediaFile(rawFile);
                      if (mf != null)
                      {
                          results.Enqueue(mf);
                      }
                  }

                  // handle all directories from input collection
                  var directories = new List<string>();
                  foreach (var source in filesOrDirsCollection.OfType<string>().Except(rawFiles).Where(IsDirectory).TakeWhile(source => !token.IsCancellationRequested))
                  {
                      directories.Add(source);
                      try
                      {
                          directories.AddRange(Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories).TakeWhile(dir => !token.IsCancellationRequested));
                      }
                      catch (Exception e)
                      {
                          // System.UnauthorizedAccessException
                          Console.WriteLine(e);
                      }
                  }
                  foreach (var rawDir in directories.Distinct().OrderBy(s => s).TakeWhile(rawDir => !token.IsCancellationRequested))
                  {
                      this.doFindFiles(token, rawDir, results);
                  }

                  return results;
              }, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);

            //this.OnPropertyChanged(() => this.CanStartSearch);

            var mediaFiles = await this.MainTask;
            this.IsWorking = false;
            return mediaFiles;
        }

        private void doFindFiles(CancellationToken token, string dir, ConcurrentQueue<IMediaFile> results)
        {
            foreach (var extension in this.extensions.TakeWhile(rawDir => !token.IsCancellationRequested))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*" + extension).TakeWhile(rawDir => !token.IsCancellationRequested))
                    {
                        var mf = this.GetMediaFile(file);
                        if (mf != null)
                        {
                            results.Enqueue(mf);
                        }
                    }
                }
                catch (Exception e)
                {
                    // System.UnauthorizedAccessException
                    Console.WriteLine(e);
                }
            }
        }

        private IMediaFile GetMediaFile(string fileName)
        {
            if (this.IsAudioFile(fileName) && this.createMediaFileFunc != null)
            {
                try
                {
                    return this.createMediaFileFunc(fileName);
                }
                catch (Exception e)
                {
                    var em = e.Message;
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        private bool IsAudioFile(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrEmpty(ext) && this.extensions.Contains(ext.ToLower());
        }

        private static bool IsDirectory(string dirName)
        {
            return Directory.Exists(dirName);
        }
    }
}