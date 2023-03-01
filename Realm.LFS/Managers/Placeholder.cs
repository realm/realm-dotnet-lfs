using MongoDB.Bson;
using System;
using System.IO;

namespace Realms.LFS
{
    /// <summary>
    /// A class exposin functionality for generating placeholders. This will be used
    /// whenever a <see cref="FileData"/> is encountered and we don't have a local version
    /// of the file or the creator of the <see cref="FileData"/> hasn't completed uploading
    /// the remote file yet.
    /// </summary>
    public class Placeholder
    {
        private static readonly ObjectId PlaceholderId = ObjectId.Empty;

        private readonly Func<string, Stream> _generator;

        private Placeholder(Func<string, Stream> generator)
        {
            _generator = generator;
        }

        /// <summary>
        /// Constructs a <see cref="Placeholder"/> from a stream containing the image.
        /// </summary>
        /// <param name="stream">A <see cref="Stream"/> that contains the image to be used as a placeholder</param>
        /// <returns>
        /// A <see cref="Placeholder"/> instance that returns the same image regardless of the file name.
        /// </returns>
        public static Placeholder FromStream(Stream stream)
        {
            FileManager.WriteFile(FileLocation.System, PlaceholderId, stream);
            return new Placeholder((_) => FileManager.ReadFile(FileLocation.System, PlaceholderId));
        }

        /// <summary>
        /// Constructs a <see cref="Placeholder"/> from a local file.
        /// </summary>
        /// <param name="file">The absolute path to the file containing the image.</param>
        /// <returns>
        /// A <see cref="Placeholder"/> instance that returns the same image regardless of the file name.
        /// </returns>
        public static Placeholder FromFile(string file)
        {
            FileManager.CopyFile(FileLocation.System, PlaceholderId, file);
            return new Placeholder((_) => FileManager.ReadFile(FileLocation.System, PlaceholderId));
        }

        /// <summary>
        /// Constructs a <see cref="Placeholder"/> from a generator function.
        /// </summary>
        /// <param name="generator">
        /// The generator function that returns a potentially different stream based on the file name.
        /// </param>
        /// <returns>
        /// A <see cref="Placeholder"/> instance that returns a different image based on the file name.
        /// </returns>
        public static Placeholder FromGenerator(Func<string, Stream> generator)
        {
            return new Placeholder(generator);
        }

        internal Stream GeneratePlaceholder(string name) => _generator(name);
    }
}
