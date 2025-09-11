Imports System.ComponentModel
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports ProtoBuf
Imports SevenZip
Imports SharpCompress.Archives
Imports SharpCompress.Common
Imports SharpCompress.Compressors.BZip2
Imports SharpCompress.Compressors.LZMA
Imports SharpCompress.Readers
Imports SharpCompress.Writers
Imports SharpCompress.Writers.Tar
Imports SharpCompress.Writers.Zip
Imports ZstdSharp

Public Class Form1
    Delegate Sub 写入日志框委托(text As String)
    Delegate Sub 清空日志框委托()
    Delegate Sub 合并设置UI状态委托(是否启用 As Boolean)
    Delegate Sub 显示消息框委托(text As String, caption As String, buttons As MessageBoxButtons, icon As MessageBoxIcon)

    Private 客户端路径 As String
    Private 差分包路径 As String
    Private 语音差分包路径 As String

    Private 旧客户端路径 As String
    Private 新客户端路径 As String
    Private 差分包保存路径 As String

    Private 差分包是否压缩包 As Boolean = False
    Private 语音差分包是否压缩包 As Boolean = False

    Private V2差分包 As Boolean = False
    Private V2语音差分包 As Boolean = False

    Private Ldiff差分包 As Boolean = False
    Private Ldiff语音差分包 As Boolean = False

    Private hpatchzExe As String
    Private hdiffzExe As String

    Private 任务是否正在运行 As Boolean = False
    Private 检查任务是否正在运行 As Boolean = False

    Private 压缩包密码字典 As New Dictionary(Of String, String)

    Private 成员_自动调整控件大小 As 自动调整控件大小

    Private 七ZipDll As String

    Public Sub 写入日志框(text As String)
        If TextBox4.InvokeRequired Then
            TextBox4.Invoke(New 写入日志框委托(AddressOf 写入日志框), text)
        Else
            TextBox4.AppendText(text & Environment.NewLine)
            TextBox4.ScrollToCaret()
        End If
    End Sub

    Public Sub 清空日志框()
        If TextBox4.InvokeRequired Then
            TextBox4.Invoke(New 清空日志框委托(AddressOf 清空日志框))
        Else
            TextBox4.Clear()
        End If
    End Sub

    Private Sub 合并设置UI状态(是否启用 As Boolean)
        If Me.InvokeRequired Then
            Me.Invoke(New 合并设置UI状态委托(AddressOf 设置UI状态), 是否启用)
        Else
            Button1.Enabled = 是否启用
            Button4.Enabled = 是否启用 AndAlso CheckBox1.Checked
            Button6.Enabled = 是否启用 AndAlso CheckBox1.Checked
            CheckBox1.Enabled = 是否启用
            Button2.Enabled = 是否启用
            Button3.Enabled = 是否启用
            Button5.Enabled = 是否启用
        End If
    End Sub

    Private Sub 显示消息框(text As String, caption As String, buttons As MessageBoxButtons, icon As MessageBoxIcon)
        If Me.InvokeRequired Then
            Me.Invoke(New 显示消息框委托(AddressOf 显示消息框), text, caption, buttons, icon)
        Else
            MessageBox.Show(text, caption, buttons, icon)
        End If
    End Sub

    Private Sub 执行CMD(cmd As String)
        Dim p As New Process()
        p.StartInfo.FileName = cmd
        p.StartInfo.RedirectStandardOutput = True
        p.StartInfo.RedirectStandardError = True
        p.StartInfo.UseShellExecute = False
        p.StartInfo.CreateNoWindow = True
        p.Start()

        Dim stdOutput As String = p.StandardOutput.ReadToEnd()
        Dim errorOutput As String = p.StandardError.ReadToEnd()
        p.WaitForExit()

        If Not String.IsNullOrEmpty(stdOutput) Then
            写入日志框(stdOutput)
        End If

        If Not String.IsNullOrEmpty(errorOutput) Then
            写入日志框(errorOutput)
        End If
    End Sub

    Private Sub 删除只读属性(path As String)
        For Each file As String In Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            Dim fileInfo As New FileInfo(file)
            fileInfo.Attributes = FileAttributes.Normal
        Next
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
                        Dim 输入密码 As String = InputBox($"压缩包 {Path.GetFileName(zipFilePath)} 已加密，请输入密码:", "需要密码")
                        If String.IsNullOrEmpty(输入密码) Then
                            MessageBox.Show("操作已取消，无法解压加密的差分包。", "警告：", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Return False
                        End If
                        密码 = 输入密码
                        GoTo 重试标签
                    Else
                        Throw New NotSupportedException($"不支持的压缩格式: {扩展名}")
                    End If
                End Try
            End If
        Catch ex As Exception
            写入日志框("检查压缩包时出错：" & ex.Message)
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
                            Dim 输入密码 As String = InputBox($"压缩包 {Path.GetFileName(zipFilePath)} 已加密，请输入密码:", "需要密码")
                            If String.IsNullOrEmpty(输入密码) Then
                                MessageBox.Show("操作已取消，无法解压加密压缩包。", "警告：", MessageBoxButtons.OK, MessageBoxIcon.Warning)
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
                        Dim 输入密码 As String = InputBox($"压缩包 {Path.GetFileName(zipFilePath)} 已加密，请输入密码:", "需要密码")
                        If String.IsNullOrEmpty(输入密码) Then
                            MessageBox.Show("操作已取消，无法解压加密压缩包。", "警告：", MessageBoxButtons.OK, MessageBoxIcon.Warning)
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
                        Dim 输入密码 As String = InputBox($"压缩包 {Path.GetFileName(zipFilePath)} 已加密，请输入密码:", "需要密码")
                        If String.IsNullOrEmpty(输入密码) Then
                            MessageBox.Show("操作已取消，无法解压加密压缩包。", "警告：", MessageBoxButtons.OK, MessageBoxIcon.Warning)
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
            写入日志框("解压压缩包时出错：" & ex.Message)
            Throw
        End Try
    End Sub

    Private Async Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        If 任务是否正在运行 OrElse 检查任务是否正在运行 Then
            MessageBox.Show("当前已有任务正在运行，请等待完成后再试。", "警告：", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not File.Exists("hpatchz.exe") Then
            MessageBox.Show("hpatchz.exe 文件不存在于程序路径下！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        客户端路径 = TextBox2.Text
        差分包路径 = TextBox1.Text
        语音差分包路径 = TextBox3.Text
        hpatchzExe = Path.Combine(Application.StartupPath, "hpatchz.exe")

        If String.IsNullOrEmpty(客户端路径) OrElse String.IsNullOrEmpty(差分包路径) OrElse (CheckBox1.Checked AndAlso String.IsNullOrEmpty(语音差分包路径)) Then
            MessageBox.Show("路径不能为空！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If CheckBox1.Checked Then
            Dim result As DialogResult = MessageBox.Show("请确认你所填的路径是否正确：" & vbCrLf & vbCrLf & "客户端路径：" & 客户端路径 & vbCrLf & "游戏差分包路径：" & 差分包路径 & vbCrLf & "语音差分包路径：" & 语音差分包路径 & vbCrLf & vbCrLf & "填写不正确的路径会导致合并失败，合并失败只能重新解压重来！", "警告：", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
            If result = DialogResult.Cancel Then Return
        Else
            Dim result As DialogResult = MessageBox.Show("请确认你所填的路径是否正确：" & vbCrLf & vbCrLf & "客户端路径：" & 客户端路径 & vbCrLf & "游戏差分包路径：" & 差分包路径 & vbCrLf & vbCrLf & "填写不正确的路径会导致合并失败，合并失败只能重新解压重来！", "警告：", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
            If result = DialogResult.Cancel Then Return
        End If

        Button1.Enabled = False
        检查任务是否正在运行 = True
        清空日志框()
        合并设置UI状态(False)

        Dim 检查结果 As Boolean = Await Task.Run(Function() 检查差分包())

        If Not 检查结果 Then
            检查任务是否正在运行 = False
            合并设置UI状态(True)
            Return
        End If

        任务是否正在运行 = True
        检查任务是否正在运行 = False
        BackgroundWorker1.RunWorkerAsync()
    End Sub

    Private Function 检查差分包() As Boolean
        Try
            写入日志框("正在检查差分包，请稍候...")

            If Not 差分包是否压缩包 Then
                If File.Exists(Path.Combine(差分包路径, "manifest")) Then
                    Ldiff差分包 = True
                Else
                    If Not File.Exists(Path.Combine(差分包路径, "deletefiles.txt")) Then
                        显示消息框("差分包文件不存在！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Return False
                    ElseIf File.Exists(Path.Combine(差分包路径, "hdifffiles.txt")) Then
                        V2差分包 = False
                    ElseIf File.Exists(Path.Combine(差分包路径, "hdiffmap.json")) Then
                        V2差分包 = True
                    Else
                        显示消息框("差分包文件不存在！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Return False
                    End If
                End If
            Else
                If 检查压缩包中的文件(差分包路径, "manifest") Then
                    Ldiff差分包 = True
                Else
                    If Not 检查压缩包中的文件(差分包路径, "deletefiles.txt") Then
                        显示消息框("差分包文件不存在或不正确！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Return False
                    ElseIf 检查压缩包中的文件(差分包路径, "hdifffiles.txt") Then
                        V2差分包 = False
                    ElseIf 检查压缩包中的文件(差分包路径, "hdiffmap.json") Then
                        V2差分包 = True
                    Else
                        显示消息框("差分包文件不存在或不正确！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Return False
                    End If
                End If
            End If

            If CheckBox1.Checked Then
                写入日志框("正在检查语音差分包，请稍候...")

                If Not 语音差分包是否压缩包 Then
                    If File.Exists(Path.Combine(语音差分包路径, "manifest")) Then
                        Ldiff语音差分包 = True
                    Else
                        If Not File.Exists(Path.Combine(语音差分包路径, "deletefiles.txt")) Then
                            显示消息框("语音差分包文件不存在！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Return False
                        ElseIf File.Exists(Path.Combine(语音差分包路径, "hdifffiles.txt")) Then
                            V2语音差分包 = False
                        ElseIf File.Exists(Path.Combine(语音差分包路径, "hdiffmap.json")) Then
                            V2语音差分包 = True
                        Else
                            显示消息框("语音差分包文件不存在！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Return False
                        End If
                    End If
                Else
                    If 检查压缩包中的文件(语音差分包路径, "manifest") Then
                        Ldiff语音差分包 = True
                    Else
                        If Not 检查压缩包中的文件(语音差分包路径, "deletefiles.txt") Then
                            显示消息框("语音差分包文件不存在或不正确！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Return False
                        ElseIf 检查压缩包中的文件(语音差分包路径, "hdifffiles.txt") Then
                            V2语音差分包 = False
                        ElseIf 检查压缩包中的文件(语音差分包路径, "hdiffmap.json") Then
                            V2语音差分包 = True
                        Else
                            显示消息框("语音差分包文件不存在或不正确！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Return False
                        End If
                    End If
                End If
            End If

            写入日志框("差分包检查完成，开始合并")
            Return True
        Catch ex As Exception
            写入日志框("检查差分包时发生错误: " & ex.Message)
            显示消息框("检查差分包时发生错误: " & ex.Message, "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        成员_自动调整控件大小 = New 自动调整控件大小()
        成员_自动调整控件大小.注册窗体控件(Me)

        If IntPtr.Size = 8 Then ' 64位
            七ZipDll = "x64\\7z.dll"
        Else ' 32位
            七ZipDll = "x86\\7z.dll"
        End If

        If Not CheckBox1.Checked Then
            Button4.Enabled = False
            TextBox3.Enabled = False
            Label3.Enabled = False
            Button6.Enabled = False
        End If

        ' 确保事件只绑定一次
        RemoveHandler BackgroundWorker1.DoWork, AddressOf BackgroundWorker1_DoWork
        RemoveHandler BackgroundWorker1.RunWorkerCompleted, AddressOf BackgroundWorker1_RunWorkerCompleted
        RemoveHandler BackgroundWorker2.DoWork, AddressOf BackgroundWorker2_DoWork
        RemoveHandler BackgroundWorker2.RunWorkerCompleted, AddressOf BackgroundWorker2_RunWorkerCompleted

        AddHandler BackgroundWorker1.DoWork, AddressOf BackgroundWorker1_DoWork
        AddHandler BackgroundWorker1.RunWorkerCompleted, AddressOf BackgroundWorker1_RunWorkerCompleted
        AddHandler BackgroundWorker2.DoWork, AddressOf BackgroundWorker2_DoWork
        AddHandler BackgroundWorker2.RunWorkerCompleted, AddressOf BackgroundWorker2_RunWorkerCompleted
    End Sub

    Private Sub BackgroundWorker1_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs)
        Try
            If 差分包是否压缩包 Then
                Dim 压缩文件目录 As String = Path.GetDirectoryName(差分包路径)
                Dim 压缩文件名 As String = Path.GetFileNameWithoutExtension(差分包路径)
                Dim 压缩文件路径 As String = 差分包路径
                差分包路径 = 压缩文件目录 & "\" & 压缩文件名
                If Directory.Exists(差分包路径) Then Directory.Delete(差分包路径, True)
                写入日志框("解压：" & 压缩文件路径 & "...")
                解压压缩包(压缩文件路径, 差分包路径)
            End If

            If CheckBox1.Checked And 语音差分包是否压缩包 Then
                Dim 压缩文件目录 As String = Path.GetDirectoryName(语音差分包路径)
                Dim 压缩文件名 As String = Path.GetFileNameWithoutExtension(语音差分包路径)
                Dim 压缩文件路径 As String = 语音差分包路径
                语音差分包路径 = 压缩文件目录 & "\" & 压缩文件名
                If Directory.Exists(语音差分包路径) Then Directory.Delete(语音差分包路径, True)
                写入日志框("解压：" & 压缩文件路径 & "...")
                解压压缩包(压缩文件路径, 语音差分包路径)
            End If

            If Ldiff差分包 Then
                差分包路径 = Ldiff转换(差分包路径, 客户端路径, 差分包是否压缩包)
                V2差分包 = True
            End If

            If CheckBox1.Checked And Ldiff语音差分包 Then
                语音差分包路径 = Ldiff转换(语音差分包路径, 客户端路径, 语音差分包是否压缩包)
                V2语音差分包 = True
            End If

            Dim deleteFiles As List(Of String)
            If File.Exists(Path.Combine(差分包路径, "deletefiles.txt")) Then
                deleteFiles = File.ReadLines(Path.Combine(差分包路径, "deletefiles.txt")).ToList()
            Else
                deleteFiles = New List(Of String)()
            End If

            Dim deleteFilesAudio As List(Of String)
            If CheckBox1.Checked Then
                If File.Exists(Path.Combine(语音差分包路径, "deletefiles.txt")) Then
                    deleteFilesAudio = File.ReadLines(Path.Combine(语音差分包路径, "deletefiles.txt")).ToList()
                Else
                    deleteFilesAudio = New List(Of String)()
                End If
            Else
                deleteFilesAudio = New List(Of String)()
            End If

            删除只读属性(客户端路径)
            删除只读属性(差分包路径)
            If CheckBox1.Checked Then 删除只读属性(语音差分包路径)

            Dim temp目录 As String = Path.Combine(客户端路径, "temp")
            Directory.CreateDirectory(temp目录)

            删除文件(客户端路径, deleteFiles)
            应用补丁(客户端路径, 差分包路径, temp目录, V2差分包)
            移动文件(差分包路径, 客户端路径)
            Directory.Delete(差分包路径, True)

            If CheckBox1.Checked Then
                删除文件(客户端路径, deleteFilesAudio)
                应用补丁(客户端路径, 语音差分包路径, temp目录, V2语音差分包)
                移动文件(语音差分包路径, 客户端路径)
                Directory.Delete(语音差分包路径, True)
            End If

            写入日志框("合并完成!")
        Catch ex As Exception
            写入日志框(ex.ToString())
        End Try
    End Sub

    Private Sub BackgroundWorker1_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs)
        合并设置UI状态(True)
        任务是否正在运行 = False

        If e.Error IsNot Nothing OrElse TypeOf e.Result Is Exception Then
            MessageBox.Show("合并过程中发生错误，请检查日志！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Else
            MessageBox.Show("合并操作成功完成！", "信息：", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub 应用补丁(客户端目录 As String, 差分包目录 As String, 临时目录 As String, IsV2 As Boolean)
        If Not IsV2 Then
            Dim hdiffFiles As List(Of String) = File.ReadLines(Path.Combine(差分包目录, "hdifffiles.txt")).ToList()
            For Each line As String In hdiffFiles
                Dim patchInfo As JsonObject = JsonSerializer.Deserialize(Of JsonObject)(line)
                Dim remoteName As String = patchInfo("remoteName").ToString()
                Dim baseName As String = Path.GetFileName(remoteName)

                写入日志框($"合并：{baseName}...")

                Dim 源文件 As String = Path.Combine(客户端目录, remoteName)
                Dim hdiff文件 As String = Path.Combine(差分包目录, remoteName & ".hdiff")
                Dim 目标文件 As String = Path.Combine(临时目录, baseName)

                Dim cmd As String = $"""{hpatchzExe}"" ""{源文件}"" ""{hdiff文件}"" ""{目标文件}"""
                执行CMD(cmd)

                If File.Exists(源文件) Then File.Delete(源文件)
                If File.Exists(hdiff文件) Then File.Delete(hdiff文件)
                File.Move(目标文件, 源文件)
            Next
        Else
            Dim hdiffFiles As HDiffMap = JsonSerializer.Deserialize(Of HDiffMap)(File.ReadAllText(Path.Combine(差分包目录, "hdiffmap.json")))
            For Each json As HDiffData In hdiffFiles.DiffMap
                Dim sourceFileName As String = json.SourceFileName
                Dim targetFileName As String = json.TargetFileName
                Dim patchFileName As String = json.PatchFileName

                写入日志框($"合并：{targetFileName}...")

                Dim 源文件 As String = Path.Combine(客户端目录, sourceFileName)
                Dim hdiff文件 As String = Path.Combine(差分包目录, patchFileName)
                Dim 目标文件 As String = Path.Combine(客户端目录, targetFileName)
                Dim 临时文件 As String = Path.Combine(临时目录, Path.GetFileName(targetFileName))

                If Not File.Exists(hdiff文件) Then
                    写入日志框("警告：找不到hdiff文件：" & hdiff文件)
                    Continue For
                End If

                Dim cmd As String
                If Not sourceFileName = "" Then
                    If Not File.Exists(源文件) Then
                        写入日志框("警告：找不到源文件：" & 源文件)
                        If File.Exists(hdiff文件) Then File.Delete(hdiff文件)
                        Continue For
                    End If

                    cmd = $"""{hpatchzExe}"" ""{源文件}"" ""{hdiff文件}"" ""{临时文件}"""
                Else
                    cmd = $"""{hpatchzExe}"" -f """" ""{hdiff文件}"" ""{临时文件}"""
                End If
                执行CMD(cmd)

                If File.Exists(源文件) And Not sourceFileName = "" Then File.Delete(源文件)
                If File.Exists(hdiff文件) Then File.Delete(hdiff文件)
                If File.Exists(目标文件) Then File.Delete(目标文件)
                File.Move(临时文件, 目标文件)
            Next
        End If
    End Sub

    Private Sub 删除文件(文件路径 As String, 文件列表 As List(Of String))
        For Each df As String In 文件列表
            Dim filePath As String = Path.Combine(文件路径, df.Trim())
            If File.Exists(filePath) Then
                File.Delete(filePath)
                写入日志框($"删除：{filePath}")
            End If
        Next
    End Sub

    Private Sub 移动文件(源路径 As String, 目标路径 As String)
        For Each 源文件 As String In Directory.GetFiles(源路径, "*", SearchOption.AllDirectories)
            Dim 相对路径 As String = 源文件.Substring(源路径.Length + 1)
            Dim 目标文件 As String = Path.Combine(目标路径, 相对路径)
            Dim 目的文件夹 As String = Path.GetDirectoryName(目标文件)
            If Not Directory.Exists(目的文件夹) Then
                Directory.CreateDirectory(目的文件夹)
            End If
            写入日志框($"移动：{源文件} -> {目标文件}")
            If File.Exists(目标文件) Then File.Delete(目标文件)
            File.Move(源文件, 目标文件)
        Next
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim fbd As New FolderBrowserDialog()
        If fbd.ShowDialog() = DialogResult.OK Then
            Dim fp As String = fbd.SelectedPath
            TextBox2.Text = fp
        End If
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Dim fbd As New FolderBrowserDialog()
        If fbd.ShowDialog() = DialogResult.OK Then
            Dim fp As String = fbd.SelectedPath
            差分包是否压缩包 = False
            TextBox1.Text = fp
        End If
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Dim fbd As New FolderBrowserDialog()
        If fbd.ShowDialog() = DialogResult.OK Then
            Dim fp As String = fbd.SelectedPath
            语音差分包是否压缩包 = False
            TextBox3.Text = fp
        End If
    End Sub

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged
        If CheckBox1.Checked Then
            Button4.Enabled = True
            TextBox3.Enabled = True
            Label3.Enabled = True
            Button6.Enabled = True
        Else
            Button4.Enabled = False
            TextBox3.Enabled = False
            Label3.Enabled = False
            Button6.Enabled = False
        End If
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        Dim ofd As New OpenFileDialog()
        ofd.Multiselect = False
        ofd.Filter = "压缩包文件 (*.zip;*.7z;*.rar;*.tar.lz;*.tar.bz2)|*.zip;*.7z;*.rar;*.tar.lz;*.tar.bz2|所有文件 (*.*)|*.*"

        If ofd.ShowDialog() = DialogResult.OK Then
            Dim fp As String = ofd.FileName
            差分包是否压缩包 = True
            TextBox1.Text = fp
        End If
    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click
        Dim ofd As New OpenFileDialog()
        ofd.Multiselect = False
        ofd.Filter = "压缩包文件 (*.zip;*.7z;*.rar;*.tar.lz;*.tar.bz2)|*.zip;*.7z;*.rar;*.tar.lz;*.tar.bz2|所有文件 (*.*)|*.*"

        If ofd.ShowDialog() = DialogResult.OK Then
            Dim fp As String = ofd.FileName
            语音差分包是否压缩包 = True
            TextBox3.Text = fp
        End If
    End Sub

    Private Sub 制作选择旧客户端按钮_Click(sender As Object, e As EventArgs) Handles 制作选择旧客户端按钮.Click
        Dim fbd As New FolderBrowserDialog()
        If fbd.ShowDialog() = DialogResult.OK Then
            旧客户端路径框.Text = fbd.SelectedPath
        End If
    End Sub

    Private Sub 制作选择新客户端_Click(sender As Object, e As EventArgs) Handles 制作选择新客户端.Click
        Dim fbd As New FolderBrowserDialog()
        If fbd.ShowDialog() = DialogResult.OK Then
            新客户端路径框.Text = fbd.SelectedPath
        End If
    End Sub

    Private Sub 制作选择差分包_Click(sender As Object, e As EventArgs) Handles 制作选择差分包.Click
        Dim sfd As New SaveFileDialog()
        sfd.Filter = "zip 文件 (*.zip)|*.zip|.7z 文件 (*.7z)|*.7z|tar.lz 文件 (*.tar.lz)|*.tar.lz|tar.bz2 文件 (*.tar.bz2)|*.tar.bz2"
        sfd.FilterIndex = 0
        sfd.RestoreDirectory = True
        sfd.OverwritePrompt = True
        sfd.AddExtension = False

        If sfd.ShowDialog() = DialogResult.OK Then
            Dim fp As String = sfd.FileName
            Dim 后缀 As String() = {"", ".zip", ".7z", ".tar.lz", ".tar.bz2", ""}
            Dim 预计后缀 As String = 后缀(sfd.FilterIndex)
            If 预计后缀 <> "" Then
                While fp.EndsWith(预计后缀 & 预计后缀, StringComparison.OrdinalIgnoreCase)
                    fp = fp.Substring(0, fp.Length - 预计后缀.Length)
                End While
                If Not fp.EndsWith(预计后缀, StringComparison.OrdinalIgnoreCase) Then
                    fp &= 预计后缀
                End If
            End If
            差分包保存路径框.Text = fp
        End If
    End Sub

    Private Sub 制作按钮_Click(sender As Object, e As EventArgs) Handles 制作按钮.Click
        If 任务是否正在运行 Then
            MessageBox.Show("当前已有任务正在运行，请等待完成后再试。", "警告：", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If String.IsNullOrWhiteSpace(旧客户端路径框.Text) Then
            MessageBox.Show("请选择旧客户端目录", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If String.IsNullOrWhiteSpace(新客户端路径框.Text) Then
            MessageBox.Show("请选择新客户端目录", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If String.IsNullOrWhiteSpace(差分包保存路径框.Text) Then
            MessageBox.Show("请选择差分包保存位置", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If Not Directory.Exists(旧客户端路径框.Text) Then
            MessageBox.Show("旧客户端目录不存在", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If Not Directory.Exists(新客户端路径框.Text) Then
            MessageBox.Show("新客户端目录不存在", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        hdiffzExe = Path.Combine(Application.StartupPath, "hdiffz.exe")
        If Not File.Exists(hdiffzExe) Then
            MessageBox.Show("hdiffz.exe 文件不存在于程序路径下！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim result As DialogResult = MessageBox.Show("请确认你所填的路径是否正确：" & vbCrLf & vbCrLf & "旧客户端路径：" & 旧客户端路径框.Text & vbCrLf & "新客户端路径：" & 新客户端路径框.Text & vbCrLf & "差分包保存路径：" & 差分包保存路径框.Text & vbCrLf & vbCrLf & "填写不正确的路径会导致制作失败！", "警告：", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
        If result = DialogResult.Cancel Then Return

        设置UI状态(False)

        任务是否正在运行 = True
        清空日志框()
        BackgroundWorker2.RunWorkerAsync()
    End Sub

    Private Sub 设置UI状态(是否启用 As Boolean)
        制作选择旧客户端按钮.Enabled = 是否启用
        制作选择新客户端.Enabled = 是否启用
        制作选择差分包.Enabled = 是否启用
        制作按钮.Enabled = 是否启用
    End Sub

    Private Sub BackgroundWorker2_DoWork(sender As Object, e As DoWorkEventArgs)
        Dim 输出目录 As String = Path.GetDirectoryName(差分包保存路径框.Text)
        Dim 压缩包名称 As String = Path.GetFileName(差分包保存路径框.Text)
        Dim 临时目录 As String = Path.Combine(输出目录, Path.GetFileNameWithoutExtension(差分包保存路径框.Text))

        Try
            写入日志框($"旧版本目录: {旧客户端路径框.Text}")
            写入日志框($"新版本目录: {新客户端路径框.Text}")
            写入日志框($"输出压缩包: {差分包保存路径框.Text}")

            写入日志框($"创建临时目录: {临时目录}")
            If Directory.Exists(临时目录) Then
                Directory.Delete(临时目录, True)
            End If

            Directory.CreateDirectory(临时目录)

            Dim 旧文件集合 As HashSet(Of String) = 获取文件列表(旧客户端路径框.Text)
            Dim 新文件集合 As HashSet(Of String) = 获取文件列表(新客户端路径框.Text)

            生成删除文件列表(旧文件集合, 新文件集合, 临时目录)
            生成补丁文件(旧客户端路径框.Text, 新客户端路径框.Text, 临时目录, 旧文件集合, 新文件集合)
            添加新增文件(新客户端路径框.Text, 临时目录, 旧文件集合, 新文件集合)
            创建压缩包(临时目录, 差分包保存路径框.Text)

            Directory.Delete(临时目录, True)

            e.Result = Nothing
        Catch ex As Exception
            e.Result = ex
        End Try
    End Sub

    Private Function 获取文件列表(目录路径 As String) As HashSet(Of String)
        Dim 文件列表 As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim 文件数量 As Integer = 0

        For Each 文件路径 In Directory.GetFiles(目录路径, "*", SearchOption.AllDirectories)
            Dim 相对路径 As String = 获取相对路径(目录路径, 文件路径).Replace("\", "/")
            文件列表.Add(相对路径)
            文件数量 += 1
        Next

        Return 文件列表
    End Function

    Private Function 获取相对路径(基础路径 As String, 完整路径 As String) As String
        If Not 基础路径.EndsWith(Path.DirectorySeparatorChar) Then
            基础路径 &= Path.DirectorySeparatorChar
        End If

        Dim 基础Uri As New Uri(基础路径)
        Dim 完整Uri As New Uri(完整路径)
        Dim 相对Uri As Uri = 基础Uri.MakeRelativeUri(完整Uri)
        Return Uri.UnescapeDataString(相对Uri.ToString()).Replace("/", Path.DirectorySeparatorChar)
    End Function

    Private Sub 生成删除文件列表(旧文件集合 As HashSet(Of String), 新文件集合 As HashSet(Of String), 输出目录 As String)
        Dim 已删除文件集合 As New HashSet(Of String)(旧文件集合)
        已删除文件集合.ExceptWith(新文件集合)

        Dim 删除文件路径 As String = Path.Combine(输出目录, "deletefiles.txt")
        If File.Exists(删除文件路径) Then File.Delete(删除文件路径)

        Using 写入器 As New StreamWriter(删除文件路径)
            For Each 文件路径 In 已删除文件集合
                写入器.WriteLine(文件路径)
                写入日志框($"待删除文件：{文件路径}")
            Next
        End Using
    End Sub

    Private Sub 生成补丁文件(旧目录 As String, 新目录 As String, 输出目录 As String, 旧文件集合 As HashSet(Of String), 新文件集合 As HashSet(Of String))
        Dim 共有文件集合 As New HashSet(Of String)(旧文件集合)
        共有文件集合.IntersectWith(新文件集合)
        Dim 补丁条目列表 As New List(Of Dictionary(Of String, String))
        Dim 补丁数量 As Integer = 0
        Dim 跳过数量 As Integer = 0

        For Each 文件相对路径 In 共有文件集合
            Dim 文件名 As String = Path.GetFileName(文件相对路径)
            If 文件名 = "hdifffiles.txt" OrElse 文件名 = "deletefiles.txt" Then
                写入日志框($"跳过差分包生成文件：{文件相对路径}")
                Continue For
            End If

            Dim 旧文件路径 As String = Path.Combine(旧目录, 文件相对路径)
            Dim 新文件路径 As String = Path.Combine(新目录, 文件相对路径)

            If 文件是否相同(旧文件路径, 新文件路径) Then
                写入日志框($"跳过相同文件：{文件相对路径}")
                跳过数量 += 1
                Continue For
            End If

            Dim 补丁文件路径 As String = Path.Combine(输出目录, 文件相对路径 & ".hdiff")
            Directory.CreateDirectory(Path.GetDirectoryName(补丁文件路径))

            写入日志框($"生成差分文件：{补丁文件路径}")
            Dim cmd As String = $"""{hdiffzExe}"" ""{旧文件路径}"" ""{新文件路径}"" ""{补丁文件路径}"""
            执行CMD(cmd)

            补丁条目列表.Add(New Dictionary(Of String, String) From {{"remoteName", 文件相对路径}})
            补丁数量 += 1
        Next

        Dim 补丁列表路径 As String = Path.Combine(输出目录, "hdifffiles.txt")

        If File.Exists(补丁列表路径) Then File.Delete(补丁列表路径)
        Using 写入器 As New StreamWriter(补丁列表路径)
            For Each 条目 In 补丁条目列表
                写入器.WriteLine(JsonSerializer.Serialize(条目, New JsonSerializerOptions With {.WriteIndented = False}))
            Next
        End Using
    End Sub

    Private Function 文件是否相同(文件路径1 As String, 文件路径2 As String) As Boolean
        Dim 文件信息1 As New FileInfo(文件路径1)
        Dim 文件信息2 As New FileInfo(文件路径2)

        If 文件信息1.Length <> 文件信息2.Length Then Return False

        Using MD5计算器 As MD5 = MD5.Create()
            Dim md5文件1 As String
            Using 文件流 As FileStream = File.OpenRead(文件路径1)
                md5文件1 = BitConverter.ToString(MD5计算器.ComputeHash(文件流)).Replace("-", "").ToLowerInvariant()
            End Using

            MD5计算器.Initialize()

            Dim md5文件2 As String
            Using 文件流 As FileStream = File.OpenRead(文件路径2)
                md5文件2 = BitConverter.ToString(MD5计算器.ComputeHash(文件流)).Replace("-", "").ToLowerInvariant()
            End Using

            Return md5文件1 = md5文件2
        End Using
    End Function

    Private Sub 添加新增文件(新目录 As String, 输出目录 As String, 旧文件集合 As HashSet(Of String), 新文件集合 As HashSet(Of String))
        Dim 新增文件集合 As New HashSet(Of String)(新文件集合)
        新增文件集合.ExceptWith(旧文件集合)
        Dim 新增数量 As Integer = 0

        For Each 文件相对路径 In 新增文件集合
            Dim 文件名 As String = Path.GetFileName(文件相对路径)
            If 文件名 = "hdifffiles.txt" OrElse 文件名 = "deletefiles.txt" Then
                写入日志框($"跳过差分包生成文件：{文件相对路径}")
                Continue For
            End If

            Dim 源文件路径 As String = Path.Combine(新目录, 文件相对路径)
            Dim 目标文件路径 As String = Path.Combine(输出目录, 文件相对路径)

            Directory.CreateDirectory(Path.GetDirectoryName(目标文件路径))
            File.Copy(源文件路径, 目标文件路径, True)

            写入日志框($"新文件：{目标文件路径}")
            新增数量 += 1
        Next
    End Sub

    Private Sub 创建压缩包(输出目录 As String, 压缩包路径 As String)
        If File.Exists(压缩包路径) Then
            写入日志框("删除已存在的旧压缩包：" + 压缩包路径)
            File.Delete(压缩包路径)
        End If

        写入日志框($"正在创建压缩包: {压缩包路径}...")

        Dim 扩展名 As String = Path.GetExtension(压缩包路径).ToLowerInvariant()

        If 扩展名 = ".zip" Then
            Using 文件流 As FileStream = File.OpenWrite(压缩包路径)
                Using 压缩写入器 As IWriter = WriterFactory.Open(文件流, ArchiveType.Zip, New ZipWriterOptions(CompressionType.Deflate))
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

        写入日志框($"差分包已生成: {压缩包路径}")
    End Sub

    Private Sub BackgroundWorker2_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs)
        设置UI状态(True)
        任务是否正在运行 = False

        If e.Error IsNot Nothing Then
            MessageBox.Show($"制作失败: {e.Error.Message}", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            写入日志框(e.Error.ToString())
        ElseIf TypeOf e.Result Is Exception Then
            Dim ex As Exception = DirectCast(e.Result, Exception)
            MessageBox.Show($"制作失败: {ex.Message}", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            写入日志框(ex.ToString())
        Else
            MessageBox.Show("差分包制作成功!", "信息：", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub Form1_Resize(sender As Object, e As EventArgs) Handles MyBase.Resize
        If 成员_自动调整控件大小 IsNot Nothing Then
            成员_自动调整控件大小.调整窗体控件大小(Me)
        End If
    End Sub

    Private Function Ldiff转换(ldiff路径 As String, 输出路径 As String, 删除Ldiff As Boolean) As String
        Dim ldiff目录路径 As String = Path.Combine(ldiff路径, "ldiff")
        Dim 清单文件路径 As String = Path.Combine(ldiff路径, "manifest")
        Dim hdiff文件夹路径 As String = Path.Combine(输出路径, "hdiff")

        写入日志框("读取清单文件...")
        Dim 压缩数据 As Byte() = File.ReadAllBytes(清单文件路径)
        Dim 解压缩数据 As Byte()
        Using 解压缩器 As New Decompressor()
            解压缩数据 = 解压缩器.Unwrap(压缩数据).ToArray()
        End Using

        Dim 清单Proto As ManifestProto
        Using ms As New MemoryStream(解压缩数据)
            清单Proto = Serializer.Deserialize(Of ManifestProto)(ms)
        End Using

        Dim ldiff文件列表 As String() = Directory.GetFiles(ldiff目录路径)
        写入日志框($"找到 {ldiff文件列表.Length} 个ldiff文件")

        写入日志框("开始转换ldiff文件...")
        Dim 已处理数量 As Integer = 0
        For Each 当前Ldiff文件 In ldiff文件列表
            Dim asset名称 As String = Path.GetFileName(当前Ldiff文件)

            Dim 匹配的Assets列表 As New List(Of (AssetName As String, AssetSize As Long, Asset As AssetManifest))
            For Each assets组 In 清单Proto.Assets
                If assets组.AssetData IsNot Nothing Then
                    For Each assets In assets组.AssetData.Assets
                        If assets.ChunkFileName = asset名称 Then
                            匹配的Assets列表.Add((assets组.AssetName, assets组.AssetSize, assets))
                        End If
                    Next
                End If
            Next

            For Each 当前匹配 In 匹配的Assets列表
                Try
                    提取Ldiff文件(当前匹配.Asset, 当前匹配.AssetName, 当前匹配.AssetSize, ldiff目录路径, hdiff文件夹路径)
                    已处理数量 += 1
                Catch ex As Exception
                    写入日志框($"处理 {当前匹配.AssetName} 时出错: {ex.Message}")
                End Try
            Next
        Next
        写入日志框($"已转换 {已处理数量} 个文件")

        写入日志框("生成 hdiffMap ...")
        Dim 差异映射名称列表 As New List(Of String)(ldiff文件列表.Length)
        For Each 当前条目 In ldiff文件列表
            差异映射名称列表.Add(Path.GetFileName(当前条目))
        Next

        Dim 映射 As HDiffMap = 生成HdiffMap(清单Proto, 差异映射名称列表)
        Dim 映射JSON As String = JsonSerializer.Serialize(映射, New JsonSerializerOptions With {.WriteIndented = True})
        Dim 映射JSON路径 As String = Path.Combine(hdiff文件夹路径, "hdiffmap.json")
        File.WriteAllText(映射JSON路径, 映射JSON)

        If 删除Ldiff Then
            Directory.Delete(ldiff路径, True)
        End If

        写入日志框("Ldiff转换已完成")
        Return hdiff文件夹路径
    End Function
End Class
