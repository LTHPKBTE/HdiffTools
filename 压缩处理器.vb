Imports System.IO
Imports SharpCompress.Archives
Imports SharpCompress.Common
Imports SharpCompress.Compressors.BZip2
Imports SharpCompress.Compressors.LZMA
Imports SharpCompress.Readers
Imports SharpCompress.Writers
Imports SharpCompress.Writers.Tar
Imports SharpCompress.Writers.Zip
Imports SevenZip

Public Class 压缩包处理
    Public Delegate Sub 日志委托(text As String)
    Public Delegate Function 输入密码委托(提示信息 As String, 标题 As String) As String

    Private ReadOnly 日志回调 As 日志委托
    Private ReadOnly 输入密码回调 As 输入密码委托
    Private ReadOnly 压缩包密码字典 As New Dictionary(Of String, String)
    Private Shared 七ZipDll As String

    Public Sub New(日志回调 As 日志委托， 输入密码回调 As 输入密码委托)
        Me.日志回调 = 日志回调
        Me.输入密码回调 = 输入密码回调
    End Sub

    Private Sub 设置七ZipDll()
        If String.IsNullOrEmpty(七ZipDll) Then
            If IntPtr.Size = 8 Then ' 64位
                七ZipDll = Path.Combine(Application.StartupPath, "x64\7z.dll")
            Else ' 32位
                七ZipDll = Path.Combine(Application.StartupPath, "x86\7z.dll")
            End If
        End If

        If Not File.Exists(七ZipDll) Then
            Throw New FileNotFoundException($"找不到 7z.dll 文件: {七ZipDll}")
        End If
    End Sub

    Public Function 检查压缩包中的文件(zipFilePath As String, fileName As String) As Boolean
        Dim 扩展名 As String = Path.GetExtension(zipFilePath).ToLowerInvariant()
        Dim 密码 As String = Nothing
        If 压缩包密码字典.ContainsKey(zipFilePath) Then
            密码 = 压缩包密码字典(zipFilePath)
        End If

        Try
            If 扩展名 = ".lz" OrElse zipFilePath.EndsWith(".tar.lz", StringComparison.OrdinalIgnoreCase) Then
                Using 文件流 As FileStream = File.OpenRead(zipFilePath)
                    Using lz流 As New LZipStream(文件流, SharpCompress.Compressors.CompressionMode.Decompress)
                        Using tarReader As IReader = ReaderFactory.Open(lz流)
                            While tarReader.MoveToNextEntry()
                                If Not tarReader.Entry.IsDirectory Then
                                    Dim entryName As String = tarReader.Entry.Key.Replace("/", Path.DirectorySeparatorChar)
                                    If entryName.Equals(fileName, StringComparison.OrdinalIgnoreCase) Then
                                        Return True
                                    End If
                                End If
                            End While
                        End Using
                    End Using
                End Using
            ElseIf 扩展名 = ".bz2" OrElse zipFilePath.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) Then
                Using 文件流 As FileStream = File.OpenRead(zipFilePath)
                    Using bz2流 As New BZip2Stream(文件流, SharpCompress.Compressors.CompressionMode.Decompress, False)
                        Using tarReader As IReader = ReaderFactory.Open(bz2流)
                            While tarReader.MoveToNextEntry()
                                If Not tarReader.Entry.IsDirectory Then
                                    Dim entryName As String = tarReader.Entry.Key.Replace("/", Path.DirectorySeparatorChar)
                                    If entryName.Equals(fileName, StringComparison.OrdinalIgnoreCase) Then
                                        Return True
                                    End If
                                End If
                            End While
                        End Using
                    End Using
                End Using
            ElseIf 扩展名 = ".zip" OrElse 扩展名 = ".7z" OrElse 扩展名 = ".rar" Then
重试标签:
                Dim 选项 As New ReaderOptions()
                If Not String.IsNullOrEmpty(密码) Then
                    选项.Password = 密码
                End If

                Try
                    Using archive As IArchive = ArchiveFactory.Open(zipFilePath, 选项)
                        For Each entry In archive.Entries
                            If Not entry.IsDirectory Then
                                Dim entryName As String = entry.Key.Replace("/", Path.DirectorySeparatorChar)
                                If entryName.Equals(fileName, StringComparison.OrdinalIgnoreCase) Then
                                    If Not String.IsNullOrEmpty(选项.Password) Then
                                        压缩包密码字典(zipFilePath) = 选项.Password
                                    End If
                                    Return True
                                End If
                            End If
                        Next
                    End Using
                Catch ex As Exception
                    If ex.Message.Contains("password") Then
                        Dim 输入密码 As String = 输入密码回调($"压缩包 {Path.GetFileName(zipFilePath)} 已加密，请输入密码:", "需要密码")
                        If String.IsNullOrEmpty(输入密码) Then
                            Throw New OperationCanceledException("用户取消密码输入")
                        End If
                        密码 = 输入密码
                        GoTo 重试标签
                    Else
                        Throw New NotSupportedException($"不支持的压缩格式: {扩展名}")
                    End If
                End Try
            End If
        Catch ex As Exception
            日志回调("检查压缩包时出错：" & ex.Message)
            Return False
        End Try

        Return False
    End Function

    Public Sub 解压压缩包(zipFilePath As String, extractTo As String)
        Try
            If Not Directory.Exists(extractTo) Then Directory.CreateDirectory(extractTo)
            Dim 扩展名 As String = Path.GetExtension(zipFilePath).ToLowerInvariant()
            Dim 密码 As String = Nothing

            If 压缩包密码字典.ContainsKey(zipFilePath) Then
                密码 = 压缩包密码字典(zipFilePath)
            End If

            If 扩展名 = ".lz" OrElse zipFilePath.EndsWith(".tar.lz", StringComparison.OrdinalIgnoreCase) Then
                Using 文件流 As FileStream = File.OpenRead(zipFilePath)
                    Using lz流 As New LZipStream(文件流, SharpCompress.Compressors.CompressionMode.Decompress)
                        Using tar读取器 As IReader = ReaderFactory.Open(lz流)
                            While tar读取器.MoveToNextEntry()
                                If Not tar读取器.Entry.IsDirectory Then
                                    Dim 目标路径 As String = Path.Combine(extractTo, tar读取器.Entry.Key.Replace("/", Path.DirectorySeparatorChar))
                                    Directory.CreateDirectory(Path.GetDirectoryName(目标路径))
                                    tar读取器.WriteEntryTo(目标路径)
                                End If
                            End While
                        End Using
                    End Using
                End Using
            ElseIf 扩展名 = ".bz2" OrElse zipFilePath.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) Then
                Using 文件流 As FileStream = File.OpenRead(zipFilePath)
                    Using bz2流 As New BZip2Stream(文件流, SharpCompress.Compressors.CompressionMode.Decompress, False)
                        Using tar读取器 As IReader = ReaderFactory.Open(bz2流)
                            While tar读取器.MoveToNextEntry()
                                If Not tar读取器.Entry.IsDirectory Then
                                    Dim 目标路径 As String = Path.Combine(extractTo, tar读取器.Entry.Key.Replace("/", Path.DirectorySeparatorChar))
                                    Directory.CreateDirectory(Path.GetDirectoryName(目标路径))
                                    tar读取器.WriteEntryTo(目标路径)
                                End If
                            End While
                        End Using
                    End Using
                End Using
            ElseIf 扩展名 = ".7z" Then
                设置七ZipDll()
                SevenZipExtractor.SetLibraryPath(七ZipDll)

                Dim 需要密码 As Boolean = False
                Using archive As IArchive = ArchiveFactory.Open(zipFilePath)
                    For Each entry In archive.Entries
                        If entry.IsEncrypted Then
                            需要密码 = True
                            Exit For
                        End If
                    Next
                End Using

                Dim 输入的密码 As String = 密码
重试7z:
                Try
                    If 需要密码 Then
                        If String.IsNullOrEmpty(输入的密码) Then
                            Dim 输入密码 As String = 输入密码回调($"压缩包 {Path.GetFileName(zipFilePath)} 已加密，请输入密码：", "需要密码")
                            If String.IsNullOrEmpty(输入密码) Then
                                Throw New OperationCanceledException("用户取消密码输入")
                            End If
                            输入的密码 = 输入密码
                        End If
                        Using extractor As New SevenZipExtractor(zipFilePath, 输入的密码)
                            extractor.ExtractArchive(extractTo)
                        End Using
                        压缩包密码字典(zipFilePath) = 输入的密码
                    Else
                        Using extractor As New SevenZipExtractor(zipFilePath)
                            extractor.ExtractArchive(extractTo)
                        End Using
                    End If

                Catch ex As Exception
                    If ex.Message.Contains("password") Then
                        Dim 输入密码 As String = 输入密码回调($"压缩包 {Path.GetFileName(zipFilePath)} 已加密，请输入密码:", "需要密码")
                        If String.IsNullOrEmpty(输入密码) Then
                            Throw New OperationCanceledException("用户取消密码输入")
                        End If
                        输入的密码 = 输入密码
                        GoTo 重试7z
                    Else
                        Throw
                    End If
                End Try
            ElseIf 扩展名 = ".zip" OrElse 扩展名 = ".rar" Then
                Dim 输入的密码 As String = 密码
重试其他:
                Try
                    If String.IsNullOrEmpty(输入的密码) Then
                        Using archive As IArchive = ArchiveFactory.Open(zipFilePath)
                            For Each entry In archive.Entries
                                If Not entry.IsDirectory Then
                                    entry.WriteToDirectory(extractTo, New ExtractionOptions With {
                                    .ExtractFullPath = True,
                                    .Overwrite = True
                                })
                                End If
                            Next
                        End Using
                    Else
                        Dim 选项 As New ReaderOptions With {.Password = 输入的密码}
                        Using archive As IArchive = ArchiveFactory.Open(zipFilePath, 选项)
                            For Each entry In archive.Entries
                                If Not entry.IsDirectory Then
                                    entry.WriteToDirectory(extractTo, New ExtractionOptions With {
                                    .ExtractFullPath = True,
                                    .Overwrite = True
                                })
                                End If
                            Next
                        End Using
                        压缩包密码字典(zipFilePath) = 输入的密码
                    End If

                Catch ex As Exception
                    If ex.Message.Contains("password") Then
                        Dim 输入密码 As String = 输入密码回调($"压缩包 {Path.GetFileName(zipFilePath)} 已加密，请输入密码:", "需要密码")
                        If String.IsNullOrEmpty(输入密码) Then
                            Throw New OperationCanceledException("用户取消密码输入")
                        End If
                        输入的密码 = 输入密码
                        GoTo 重试其他
                    Else
                        Throw
                    End If
                End Try

            Else
                Throw New NotSupportedException($"不支持的压缩格式: {扩展名}")
            End If

        Catch ex As Exception
            日志回调("解压压缩包时出错：" & ex.Message)
            Throw
        End Try
    End Sub

    Public Sub 创建压缩包(输出目录 As String, 压缩包路径 As String)
        If File.Exists(压缩包路径) Then
            日志回调("删除已存在的旧压缩包：" + 压缩包路径)
            File.Delete(压缩包路径)
        End If

        日志回调($"正在创建压缩包: {压缩包路径}...")

        Dim 扩展名 As String = Path.GetExtension(压缩包路径).ToLowerInvariant()

        If 扩展名 = ".zip" Then
            Using 文件流 As FileStream = File.OpenWrite(压缩包路径)
                Using 压缩写入器 As IWriter = WriterFactory.Open(文件流， ArchiveType.Zip, New ZipWriterOptions(CompressionType.Deflate))
                    For Each 文件路径 In Directory.GetFiles(输出目录, "*", SearchOption.AllDirectories)
                        Dim 相对路径 As String = 文件路径.Substring(输出目录.Length).TrimStart(Path.DirectorySeparatorChar)
                        相对路径 = 相对路径.Replace(Path.DirectorySeparatorChar, "/")
                        压缩写入器.Write(相对路径, 文件路径)
                    Next
                End Using
            End Using
        ElseIf 扩展名 = ".lz" OrElse 压缩包路径.EndsWith(".tar.lz", StringComparison.OrdinalIgnoreCase) Then
            Using 文件流 As FileStream = File.OpenWrite(压缩包路径)
                Using lz流 As New LZipStream(文件流, SharpCompress.Compressors.CompressionMode.Compress)
                    Using tar写入器 As IWriter = WriterFactory.Open(lz流, ArchiveType.Tar, New TarWriterOptions(CompressionType.None, True))
                        For Each 文件路径 In Directory.GetFiles(输出目录, "*", SearchOption.AllDirectories)
                            Dim 相对路径 As String = 文件路径.Substring(输出目录.Length).TrimStart(Path.DirectorySeparatorChar)
                            相对路径 = 相对路径.Replace(Path.DirectorySeparatorChar, "/")
                            tar写入器.Write(相对路径, 文件路径)
                        Next
                    End Using
                End Using
            End Using
        ElseIf 扩展名 = ".bz2" OrElse 压缩包路径.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) Then
            Using 文件流 As FileStream = File.OpenWrite(压缩包路径)
                Using bz2流 As New BZip2Stream(文件流, SharpCompress.Compressors.CompressionMode.Compress, False)
                    Using tar写入器 As IWriter = WriterFactory.Open(bz2流, ArchiveType.Tar, New TarWriterOptions(CompressionType.None, True))
                        For Each 文件路径 In Directory.GetFiles(输出目录, "*", SearchOption.AllDirectories)
                            Dim 相对路径 As String = 文件路径.Substring(输出目录.Length).TrimStart(Path.DirectorySeparatorChar)
                            相对路径 = 相对路径.Replace(Path.DirectorySeparatorChar, "/")
                            tar写入器.Write(相对路径, 文件路径)
                        Next
                    End Using
                End Using
            End Using
        ElseIf 扩展名 = ".7z" Then
            设置七ZipDll()
            SevenZipCompressor.SetLibraryPath(七ZipDll)
            Dim 七Z压缩器 As New SevenZipCompressor()
            七Z压缩器.CompressionMethod = CompressionMethod.Lzma2
            七Z压缩器.CompressionLevel = CompressionLevel.Ultra
            七Z压缩器.DirectoryStructure = True
            七Z压缩器.IncludeEmptyDirectories = True
            七Z压缩器.CompressDirectory(输出目录, 压缩包路径)
        Else
            Throw New NotSupportedException($"不支持的压缩格式: {扩展名}")
        End If

        日志回调($"差分包已生成: {压缩包路径}")
    End Sub
End Class
