using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReverseTextReader
{
    public class ReverseTextReader : IDisposable, IEnumerable<string>
    {
        private struct SizedBuffer
        {
            public int Size { get; }

            public byte[] Buffer { get; }

            public SizedBuffer(byte[] buffer, int size)
            {
                if (buffer == null)
                    buffer = new byte[0];

                Buffer = buffer;
                Size = size;
            }
        }
        private FileStream fs;

        // 실제 파일 커서 위치
        private long curPosF;
        public int BufferSize { get; private set; }
        private Encoding encode;

        // 파일을 읽고나서 다음번 read를 위해 남겨놓는 버퍼 블록
        private byte[] remainBuffer;

        // 이 값의 byte 수만큼 remainBuffer의 오른쪽 데이터를 이미 읽었다는 의미
        private int remainBufferRightCutF = 0;

        // UTF-8, ASCII 등에선 1바이트 단위로 비교하지만, UTF-16에선 2바이트 단위로 비교해야 함
        private readonly int compUnit;

        /// <summary>
        /// 파일의 현재 파일 커서 위치를 반환합니다. 실제 파일 커서 위치와 현재 읽고 있는 버퍼 위치를 감안하여 조정됩니다.
        /// </summary>
        public long Position => curPosF + (remainBuffer == null ? 0 : remainBuffer.Length - remainBufferRightCutF);

        /// <summary>
        /// 파일을 모두 다 읽었고, 버퍼에 남은 데이터도 없어 더 이상 읽을 내용이 없을 경우 true 입니다.
        /// </summary>
        public bool IsEOF => curPosF == 0 && remainBuffer == null;
        public bool IsDisposed { get; private set; } = false;

        public ReverseTextReader(string path, int bufferSize, Encoding encode)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException("bufferSize");

            fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 1);
            curPosF = fs.Length;
            BufferSize = bufferSize;
            this.encode = encode ?? throw new ArgumentNullException("encode");

            // \n이 2바이트를 차지하면 UTF-16
            compUnit = encode.GetByteCount("\n");
        }

        /// <summary>
        /// 파일을 마지막에서부터 읽어 줄바꿈 문자를 발견할 때까지의 string을 반환합니다.
        /// return 결과에 줄바꿈 문자는 포함되지 않습니다.
        /// </summary>
        public string ReverseRead()
        {
            return ReverseRead(Environment.NewLine);
        }

        /// <summary>
        /// 파일을 마지막에서부터 읽어 findStr을 발견할 때까지의 string을 반환합니다.
        /// return 결과에 findStr는 포함되지 않습니다.
        /// </summary>
        public string ReverseRead(string findStr)
        {
            if (IsDisposed) throw new ApplicationException("stream is disposed");

            if (IsEOF) return null;

            if (string.IsNullOrEmpty(findStr)) throw new ArgumentException("findStr");

            byte[] buffer = new byte[0];
            // findStr을 검색중인 커서 위치
            int compPos = int.MinValue;
            byte[] findByte = encode.GetBytes(findStr);
            long curPos = curPosF;
            int leftOverBlockRightCut = remainBufferRightCutF;
            bool notFoundStr;

            do
            {
                // remainBuffer에 남겨놓은 데이터가 있을 경우 해당 데이터를 먼저 처리
                // compPos == int.MinValue 으로 do-while문의 첫번째 반복인지 판별
                if (remainBuffer != null && compPos == int.MinValue)
                {
                    buffer = remainBuffer;
                    compPos = remainBuffer.Length - leftOverBlockRightCut - 1;
                }
                else
                {
                    ReadNewBlock(ref curPos);
                    leftOverBlockRightCut = 0;
                }
                notFoundStr = FindingStr(buffer, findByte);
            } while (notFoundStr && curPos > 0);

            // 맨 윗줄 라인인 경우 0번째 byte부터 다 읽어야 함
            int strStartPos = notFoundStr ? 0 : compPos + 1;
            string retValue = encode.GetString(buffer, strStartPos, buffer.Length - leftOverBlockRightCut - strStartPos);


            int leftOverBlockSize = compPos - findByte.Length + 1;

            // 현재 buffer에 남은 데이터를 다음 read를 위해 남겨놓음
            if (leftOverBlockSize > 0)
            {
                // 버퍼 크기가 정해진 크기보다 클 경우 불필요한 부분은 잘라서 저장해놓음
                // 그렇지 않을 경우 현재 새로운 buffer를 생성하지 않고 현재 buffer 그대로 사용
                if (buffer.Length > BufferSize)
                {
                    byte[] newLeftOverBlock = new byte[leftOverBlockSize];
                    Buffer.BlockCopy(buffer, 0, newLeftOverBlock, 0, leftOverBlockSize);
                    remainBuffer = newLeftOverBlock;
                    leftOverBlockRightCut = 0;
                }
                else
                {
                    remainBuffer = buffer;
                    leftOverBlockRightCut = buffer.Length - leftOverBlockSize;
                }
            }
            // 버퍼에 남겨놓을 데이터가 없을 경우 null로 설정
            else
            {
                remainBuffer = null;
                leftOverBlockRightCut = 0;
            }

            curPosF = curPos;
            remainBufferRightCutF = leftOverBlockRightCut;

            return retValue;
        }

        private byte[] ReadNewBlock(ref long curPos)
        {
            long prevCurPos = curPos;
            curPos -= BufferSize;

            if (curPos < 0)
                curPos = 0;

            int buffSize = (int)(prevCurPos - curPos);
            byte[] buffer = new byte[buffSize];

            fs.Seek(curPos, SeekOrigin.Begin);
            ReadByteFully(fs, buffer, 0, buffSize);
            return buffer;
        }

        private int ReadByteFully(FileStream fs, byte[] array, int offset, int count)
        {
            int n = 0;
            do
            {
                int readByte = fs.Read(array, offset + n, count - n);
                if (readByte == 0)
                    throw new EndOfStreamException();
                n += readByte;
            } while (n < count);

            return n;
        }

        private bool FindingStr(byte[] buffer, byte[] findByte)
        {
            int findByteLength = findByte.Length - 1;

            for (int compPos = buffer.Length - 1; compPos >= findByteLength; compPos -= compUnit)
            {
                for (int i = 0; i <= findByteLength; i++)
                {
                    if (buffer[compPos - i] != findByte[findByteLength - i])
                        goto NotFoundFindStr;
                }
                return false;
            NotFoundFindStr:;
            }

            return true;
        }

        /// <summary>
        /// 읽어들인 문자열만큼 파일을 잘라냅니다. 호출시 파일에는 아직 읽지 않은 문자열만 남게 됩니다.
        /// </summary>
        public void FileTruncateReadStr()
        {
            fs.SetLength(Position);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            fs.Dispose();
            fs = null;
            remainBuffer = null;
            encode = null;
            IsDisposed = true;
            GC.SuppressFinalize(this);
        }

        public void Reset()
        {
            curPosF = fs.Length;
            remainBuffer = null;
            remainBufferRightCutF = 0;
        }

        ~ReverseTextReader()
        {
            Dispose();
        }

        /// <summary>
        /// 파일을 마지막에서부터 읽어 findStr을 발견할 때까지의 string을 차례대로 반환합니다.
        /// </summary>
        public IEnumerable<string> GetEnumerable(string findStr)
        {
            Reset();
            return new EnumerableFindStr(this, findStr);
        }

        private struct EnumerableFindStr : IEnumerable<string>
        {
            private readonly ReverseTextReader my;
            private readonly string findStr;

            public EnumerableFindStr(ReverseTextReader my, string findStr)
            {
                this.my = my;
                this.findStr = findStr;
            }

            public IEnumerator<string> GetEnumerator()
            {
                return new ReverseTextReaderEnumerator(my, findStr);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>
        /// 파일을 마지막에서부터 읽어 줄바꿈 문자열을 발견할 때까지의 string을 차례대로 반환합니다.
        /// </summary>
        public IEnumerator<string> GetEnumerator()
        {
            Reset();
            return new ReverseTextReaderEnumerator(this, Environment.NewLine);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private struct ReverseTextReaderEnumerator : IEnumerator<string>
        {
            private readonly ReverseTextReader my;
            private readonly string findStr;

            public ReverseTextReaderEnumerator(ReverseTextReader my, string findStr)
            {
                this.my = my;
                this.findStr = findStr;
                Current = null;
            }

            public string Current { get; private set; }

            object IEnumerator.Current => Current;

            public void Dispose() => my.Dispose();

            public bool MoveNext()
            {
                Current = my.ReverseRead(findStr);

                if (Current == null)
                    return false;
                else
                    return true;
            }

            public void Reset() => my.Reset();
        }
    }
}
