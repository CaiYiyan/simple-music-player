﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jil;
using ReactiveUI;
using SchwabenCode.QuickIO;
using SimpleMusicPlayer.Core.Interfaces;
using TagLib;

namespace SimpleMusicPlayer.Core
{
    public class MediaFile : ReactiveObject, IMediaFile
    {
        private const string UNKNOWN_STRING = "<Unknown>";

        public MediaFile()
        {
        }

        public MediaFile(string fileName)
        {
            this.FullFileName = fileName;
            this.FileName = Path.GetFileName(fileName);
            this.UpdateFromTag(false);
        }

        public void UpdateFromTag(bool raisePropertyChanged = true)
        {
            var fileName = this.FullFileName;
            if (string.IsNullOrWhiteSpace(fileName) || !System.IO.File.Exists(fileName))
            {
                return;
            }

            try
            {
                using (var file = TagLib.File.Create(fileName))
                {
                    // ALBUM -> iTunes=Album, WMP10=Album, Winamp=Album
                    this.Album = file.Tag.Album;
                    if (string.IsNullOrWhiteSpace(this.Album))
                    {
                        this.Album = UNKNOWN_STRING;
                    }
                    this.AlbumSort = file.Tag.AlbumSort;

                    // ALBUMARTIST
                    var albumArtists = file.Tag.AlbumArtists.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    var albumArtistsSort = file.Tag.AlbumArtistsSort.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    this.FirstAlbumArtist = albumArtists.Count > 1 ? string.Join("/", albumArtists) : file.Tag.FirstAlbumArtist;
                    this.FirstAlbumArtistSort = albumArtistsSort.Count > 1 ? string.Join("/", albumArtistsSort) : file.Tag.FirstAlbumArtistSort;

                    // ARTIST/Performer
                    var performers = file.Tag.Performers.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    var performersSort = file.Tag.PerformersSort.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    this.FirstPerformer = performers.Count > 1 ? string.Join("/", performers) : file.Tag.FirstPerformer;
                    if (string.IsNullOrWhiteSpace(this.FirstPerformer))
                    {
                        this.FirstPerformer = UNKNOWN_STRING;
                    }
                    this.FirstPerformerSort = performersSort.Count > 1 ? string.Join("/", performersSort) : file.Tag.FirstPerformerSort;
                    if (string.IsNullOrWhiteSpace(this.FirstPerformerSort))
                    {
                        this.FirstPerformerSort = UNKNOWN_STRING;
                    }

                    // BPM
                    this.BPM = file.Tag.BeatsPerMinute;

                    // COMMENT
                    this.Comment = file.Tag.Comment;

                    // COMPOSER
                    var composers = file.Tag.Composers.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    var composersSort = file.Tag.ComposersSort.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    this.FirstComposer = composers.Count > 1 ? string.Join("/", composers) : file.Tag.FirstComposer;
                    this.FirstComposerSort = composersSort.Count > 1 ? string.Join("/", composersSort) : file.Tag.FirstComposerSort;

                    // CONDUCTOR
                    this.Conductor = file.Tag.Conductor;

                    // COPYRIGHT
                    this.Copyright = file.Tag.Copyright;

                    // TITLE
                    this.Title = file.Tag.Title;
                    this.TitleSort = file.Tag.TitleSort;

                    // GENRE
                    var genres = file.Tag.Genres.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    this.FirstGenre = genres.Count > 1 ? string.Join("/", genres) : file.Tag.FirstGenre;
                    if (string.IsNullOrWhiteSpace(this.FirstGenre))
                    {
                        this.FirstGenre = UNKNOWN_STRING;
                    }

                    this.Track = file.Tag.Track;
                    this.TrackCount = file.Tag.TrackCount;
                    this.Disc = file.Tag.Disc;
                    this.DiscCount = file.Tag.DiscCount;
                    var trackFormat = this.TrackCount > 0 ? string.Format("Track {0}/{1}", this.Track, this.TrackCount) : string.Format("Track {0}", this.Track);
                    this.TrackInfo = this.DiscCount > 0 ? string.Format("{0}  Disc {1}/{2}", trackFormat, this.Disc, this.DiscCount) : trackFormat;
                    this.Year = file.Tag.Year;
                    this.Grouping = file.Tag.Grouping;

                    var isFirstPerformerEmpty = string.IsNullOrWhiteSpace(this.FirstPerformer);
                    var isTitleEmpty = string.IsNullOrWhiteSpace(this.Title);
                    if (!isFirstPerformerEmpty && !isTitleEmpty)
                    {
                        this.FirstPerformerAndTitle = string.Concat(this.FirstPerformer, " - ", this.Title);
                    }
                    else if (!isFirstPerformerEmpty)
                    {
                        this.FirstPerformerAndTitle = this.FirstPerformer;
                    }
                    else if (!isTitleEmpty)
                    {
                        this.FirstPerformerAndTitle = this.Title;
                    }
                    else
                    {
                        this.FirstPerformerAndTitle = Path.GetFileNameWithoutExtension(this.FileName);
                    }

                    var isAlbumEmpty = string.IsNullOrWhiteSpace(this.Album);
                    if (!isFirstPerformerEmpty && !isAlbumEmpty)
                    {
                        this.FirstPerformerAndAlbum = string.Concat(this.FirstPerformer, " - ", this.Album);
                    }
                    else if (!isFirstPerformerEmpty)
                    {
                        this.FirstPerformerAndAlbum = this.FirstPerformer;
                    }
                    else if (!isAlbumEmpty)
                    {
                        this.FirstPerformerAndAlbum = this.Album;
                    }
                    else
                    {
                        this.FirstPerformerAndAlbum = Path.GetFileNameWithoutExtension(this.FileName);
                    }

                    if (file.Properties.MediaTypes != TagLib.MediaTypes.None)
                    {
                        this.Duration = file.Properties.Duration;
                        var codec = file.Properties.Codecs.FirstOrDefault(c => c is TagLib.Mpeg.AudioHeader);
                        this.IsVBR = codec != null && (((TagLib.Mpeg.AudioHeader)codec).VBRIHeader.Present || ((TagLib.Mpeg.AudioHeader)codec).XingHeader.Present);

                        this.AudioBitrate = file.Properties.AudioBitrate;
                        this.AudioSampleRate = file.Properties.AudioSampleRate;
                    }
                }
            }
            catch (Exception e)
            {
                throw new MediaFileException("TagLib.File.Create failes!", e);
            }

            if (raisePropertyChanged)
            {
                this.RaisePropertyChanged();
            }
        }

        public static IMediaFile GetMediaFileViewModel(string fileName)
        {
            var mediaFile = new MediaFile(fileName);
            return mediaFile;
        }

        [JilDirective]
        public string FirstPerformerSort { get; private set; }

        [JilDirective]
        public string FirstPerformer { get; private set; }

        [JilDirective]
        public string FirstGenre { get; private set; }

        [JilDirective]
        public string FirstComposerSort { get; private set; }

        [JilDirective]
        public string FirstComposer { get; private set; }

        [JilDirective]
        public string FirstAlbumArtistSort { get; private set; }

        [JilDirective]
        public string FirstAlbumArtist { get; private set; }

        [JilDirective]
        public string FirstPerformerAndTitle { get; private set; }

        [JilDirective]
        public string FirstPerformerAndAlbum { get; private set; }

        [JilDirective]
        public TimeSpan Duration { get; private set; }

        [JilDirective]
        public bool IsVBR { get; private set; }

        [JilDirective]
        public int AudioBitrate { get; private set; }

        [JilDirective]
        public int AudioSampleRate { get; private set; }

        [JilDirective]
        public uint DiscCount { get; private set; }

        [JilDirective]
        public uint Disc { get; private set; }

        [JilDirective]
        public uint TrackCount { get; private set; }

        [JilDirective]
        public uint Track { get; private set; }

        [JilDirective]
        public string TrackInfo { get; private set; }

        [JilDirective]
        public uint Year { get; private set; }

        [JilDirective]
        public uint BPM { get; private set; }

        [JilDirective]
        public string Copyright { get; private set; }

        [JilDirective]
        public string Comment { get; private set; }

        [JilDirective]
        public string AlbumSort { get; private set; }

        [JilDirective]
        public string Album { get; private set; }

        [JilDirective]
        public string Conductor { get; private set; }

        [JilDirective]
        public string TitleSort { get; private set; }

        [JilDirective]
        public string Title { get; private set; }

        [JilDirective]
        public string Grouping { get; private set; }

        [JilDirective]
        public string FullFileName { get; private set; }

        [JilDirective]
        public string FileName { get; private set; }

        private PlayerState state;
        [Browsable(false)]
        [JilDirective(true)]
        public PlayerState State
        {
            get { return this.state; }
            set { this.RaiseAndSetIfChanged(ref this.state, value); }
        }

        private static BitmapImage GetImageFromPictureTag(string fileName)
        {
            try
            {
                using (var file = TagLib.File.Create(fileName))
                {
                    var pictures = file.Tag.Pictures;
                    if (pictures != null)
                    {
                        var pic = pictures.FirstOrDefault(p => p.Type == PictureType.FrontCover
                                                               || p.Type == PictureType.BackCover
                                                               || p.Type == PictureType.FileIcon
                                                               || p.Type == PictureType.OtherFileIcon
                                                               || p.Type == PictureType.Media
                                                               || p.Type == PictureType.Other);
                        if (pic != null)
                        {
                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.CreateOptions = BitmapCreateOptions.DelayCreation;
                            bi.CacheOption = BitmapCacheOption.OnDemand;
                            bi.StreamSource = new MemoryStream(pic.Data.Data);
                            bi.EndInit();
                            bi.Freeze();
                            return bi;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Fail to load cover from picture tag: {0}, {1}", fileName, e);
            }
            return null;
        }

        private static BitmapImage GetImageFromDirectory(string fileName)
        {
            try
            {
                var path2Image = Path.GetDirectoryName(fileName);
                if (string.IsNullOrEmpty(path2Image))
                {
                    return null;
                }

                var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };
                var regexCoverFiles = new Regex(".*(folder|cover|front|back|band|artist).*", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var allPossibleCoverFiles = QuickIODirectory.EnumerateFiles(path2Image).Where(f => extensions.Contains(Path.GetExtension(f.Name))).ToList();
                var cover = allPossibleCoverFiles.FirstOrDefault(f => regexCoverFiles.IsMatch(f.Name));
                cover = cover ?? allPossibleCoverFiles.FirstOrDefault();
                if (cover != null)
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CreateOptions = BitmapCreateOptions.DelayCreation;
                    bi.CacheOption = BitmapCacheOption.OnDemand;
                    bi.UriSource = new Uri(cover.FullName, UriKind.RelativeOrAbsolute);
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Fail to load cover from directory: {0}, {1}", fileName, e);
            }
            return null;
        }

        [Browsable(false)]
        [JilDirective(true)]
        public BitmapImage Cover
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.FullFileName) || !System.IO.File.Exists(this.FullFileName))
                {
                    return null;
                }
                // try getting the cover by picture tag
                var image = GetImageFromPictureTag(this.FullFileName);
                // if no cover was found try getting the cover from disk
                return image ?? GetImageFromDirectory(this.FullFileName);
            }
        }

        private int playListIndex;
        [Browsable(false)]
        [JilDirective(true)]
        public int PlayListIndex
        {
            get { return this.playListIndex; }
            set { this.RaiseAndSetIfChanged(ref this.playListIndex, value); }
        }

        public override string ToString()
        {
            return string.Format("{0} {1} - {2} {3:m\\:ss}", this.Track, this.FirstAlbumArtist, this.Title, this.Duration);
        }
    }
}