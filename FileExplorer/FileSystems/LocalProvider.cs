using System;
using System.Collections.Generic;
using System.Text;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class LocalProvider : Provider
    {
        protected LocalProvider()
        {
        }

        public override string Name => throw new NotImplementedException();

        public override string Color => throw new NotImplementedException();

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            //return new LocalFileSystem();
            throw new NotImplementedException();
        }
    }
}
