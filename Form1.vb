Imports System.ComponentModel
Imports System.IO

Public Class Form1
    Delegate Sub 写入日志框委托(text As String)
    Delegate Sub 清空日志框委托()
    Delegate Sub 合并设置UI状态委托(是否启用 As Boolean)
    Delegate Sub 显示消息框委托(text As String, caption As String, buttons As MessageBoxButtons, icon As MessageBoxIcon)

    Private 差分包是否压缩包 As Boolean = False
    Private 语音差分包是否压缩包 As Boolean = False

    Private 任务是否正在运行 As Boolean = False
    Private 检查任务是否正在运行 As Boolean = False

    Private 成员_自动调整控件大小 As 自动调整控件大小
    Private 压缩器 As 压缩包处理
    Private 差分包制作器 As 差分包制作
    Private 差分包合并器 As 差分包合并

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

    Private Function InputBox委托(提示信息 As String, 标题 As String) As String
        If Me.InvokeRequired Then
            Return Me.Invoke(Function() InputBox(提示信息, 标题))
        Else
            Return InputBox(提示信息, 标题)
        End If
    End Function

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

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        成员_自动调整控件大小 = New 自动调整控件大小()
        成员_自动调整控件大小.注册窗体控件(Me)
        压缩器 = New 压缩包处理(AddressOf 写入日志框, AddressOf InputBox委托)
        差分包制作器 = New 差分包制作(AddressOf 写入日志框, AddressOf 执行CMD, 压缩器)
        差分包合并器 = New 差分包合并(AddressOf 写入日志框, AddressOf 执行CMD, 压缩器)

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

    Private Async Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        If 任务是否正在运行 OrElse 检查任务是否正在运行 Then
            MessageBox.Show("当前已有任务正在运行，请等待完成后再试。", "警告：", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim hpatchzExe As String = Path.Combine(Application.StartupPath, "hpatchz.exe")
        If Not File.Exists(hpatchzExe) Then
            MessageBox.Show("hpatchz.exe 文件不存在于程序路径下！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim 合并参数 As New 差分包合并.合并参数 With {
                .hpatchzExe = hpatchzExe,
                .客户端路径 = TextBox2.Text,
                .差分包路径 = TextBox1.Text,
                .语音差分包路径 = TextBox3.Text,
                .差分包是否压缩包 = 差分包是否压缩包,
                .语音差分包是否压缩包 = 语音差分包是否压缩包,
                .需要合并语音包 = CheckBox1.Checked
            }

        If String.IsNullOrEmpty(合并参数.客户端路径) OrElse String.IsNullOrEmpty(合并参数.差分包路径) OrElse (CheckBox1.Checked AndAlso String.IsNullOrEmpty(合并参数.语音差分包路径)) Then
            MessageBox.Show("路径不能为空！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If CheckBox1.Checked Then
            Dim result As DialogResult = MessageBox.Show("请确认你所填的路径是否正确：" & vbCrLf & vbCrLf & "客户端路径：" & 合并参数.客户端路径 & vbCrLf & "游戏差分包路径：" & 合并参数.差分包路径 & vbCrLf & "语音差分包路径：" & 合并参数.语音差分包路径 & vbCrLf & vbCrLf & "填写不正确的路径会导致合并失败，合并失败只能重新解压重来！", "警告：", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
            If result = DialogResult.Cancel Then Return
        Else
            Dim result As DialogResult = MessageBox.Show("请确认你所填的路径是否正确：" & vbCrLf & vbCrLf & "客户端路径：" & 合并参数.客户端路径 & vbCrLf & "游戏差分包路径：" & 合并参数.差分包路径 & vbCrLf & vbCrLf & "填写不正确的路径会导致合并失败，合并失败只能重新解压重来！", "警告：", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
            If result = DialogResult.Cancel Then Return
        End If

        Button1.Enabled = False
        检查任务是否正在运行 = True
        清空日志框()
        合并设置UI状态(False)

        Dim 检查结果 As Boolean = Await Task.Run(Function() 检查差分包(合并参数))

        If Not 检查结果 Then
            检查任务是否正在运行 = False
            合并设置UI状态(True)
            Return
        End If

        任务是否正在运行 = True
        检查任务是否正在运行 = False
        BackgroundWorker1.RunWorkerAsync(合并参数)
    End Sub

    Private Function 检查差分包(合并参数 As 差分包合并.合并参数) As Boolean
        Try
            写入日志框("正在检查差分包，请稍候...")

            If Not 差分包是否压缩包 Then
                If File.Exists(Path.Combine(合并参数.差分包路径, "manifest")) Then
                    合并参数.Ldiff差分包 = True
                Else
                    If Not File.Exists(Path.Combine(合并参数.差分包路径, "deletefiles.txt")) Then
                        显示消息框("差分包文件不存在！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Return False
                    ElseIf File.Exists(Path.Combine(合并参数.差分包路径, "hdifffiles.txt")) Then
                        合并参数.V2差分包 = False
                    ElseIf File.Exists(Path.Combine(合并参数.差分包路径, "hdiffmap.json")) Then
                        合并参数.V2差分包 = True
                    Else
                        显示消息框("差分包文件不存在！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Return False
                    End If
                End If
            Else
                If 压缩器.检查压缩包中的文件(合并参数.差分包路径, "manifest") Then
                    合并参数.Ldiff差分包 = True
                Else
                    If Not 压缩器.检查压缩包中的文件(合并参数.差分包路径, "deletefiles.txt") Then
                        显示消息框("差分包文件不存在或不正确！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Return False
                    ElseIf 压缩器.检查压缩包中的文件(合并参数.差分包路径, "hdifffiles.txt") Then
                        合并参数.V2差分包 = False
                    ElseIf 压缩器.检查压缩包中的文件(合并参数.差分包路径, "hdiffmap.json") Then
                        合并参数.V2差分包 = True
                    Else
                        显示消息框("差分包文件不存在或不正确！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Return False
                    End If
                End If
            End If

            If CheckBox1.Checked Then
                写入日志框("正在检查语音差分包，请稍候...")

                If Not 语音差分包是否压缩包 Then
                    If File.Exists(Path.Combine(合并参数.语音差分包路径, "manifest")) Then
                        合并参数.Ldiff语音差分包 = True
                    Else
                        If Not File.Exists(Path.Combine(合并参数.语音差分包路径, "deletefiles.txt")) Then
                            显示消息框("语音差分包文件不存在！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Return False
                        ElseIf File.Exists(Path.Combine(合并参数.语音差分包路径, "hdifffiles.txt")) Then
                            合并参数.V2语音差分包 = False
                        ElseIf File.Exists(Path.Combine(合并参数.语音差分包路径, "hdiffmap.json")) Then
                            合并参数.V2语音差分包 = True
                        Else
                            显示消息框("语音差分包文件不存在！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Return False
                        End If
                    End If
                Else
                    If 压缩器.检查压缩包中的文件(合并参数.语音差分包路径, "manifest") Then
                        合并参数.Ldiff语音差分包 = True
                    Else
                        If Not 压缩器.检查压缩包中的文件(合并参数.语音差分包路径, "deletefiles.txt") Then
                            显示消息框("语音差分包文件不存在或不正确！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Return False
                        ElseIf 压缩器.检查压缩包中的文件(合并参数.语音差分包路径, "hdifffiles.txt") Then
                            合并参数.V2语音差分包 = False
                        ElseIf 压缩器.检查压缩包中的文件(合并参数.语音差分包路径, "hdiffmap.json") Then
                            合并参数.V2语音差分包 = True
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

    Private Sub BackgroundWorker1_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs)
        Dim 合并参数 = e.Argument
        Try
            差分包合并器.合并差分包(合并参数)
            e.Result = Nothing
        Catch ex As Exception
            e.Result = ex
        End Try
    End Sub

    Private Sub BackgroundWorker1_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs)
        合并设置UI状态(True)
        任务是否正在运行 = False

        If e.Error IsNot Nothing Then
            MessageBox.Show($"合并失败: {e.Error.Message}", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            写入日志框(e.Error.ToString())
        ElseIf TypeOf e.Result Is Exception Then
            Dim ex As Exception = DirectCast(e.Result, Exception)
            MessageBox.Show($"合并失败: {ex.Message}", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            写入日志框(ex.ToString())
        Else
            MessageBox.Show("合并操作成功完成！", "信息：", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
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

        Dim hdiffzExe As String = Path.Combine(Application.StartupPath, "hdiffz.exe")
        If Not File.Exists(hdiffzExe) Then
            MessageBox.Show("hdiffz.exe 文件不存在于程序路径下！", "错误：", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim result As DialogResult = MessageBox.Show("请确认你所填的路径是否正确：" & vbCrLf & vbCrLf & "旧客户端路径：" & 旧客户端路径框.Text & vbCrLf & "新客户端路径：" & 新客户端路径框.Text & vbCrLf & "差分包保存路径：" & 差分包保存路径框.Text & vbCrLf & vbCrLf & "填写不正确的路径会导致制作失败！", "警告：", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
        If result = DialogResult.Cancel Then Return

        设置UI状态(False)

        任务是否正在运行 = True
        清空日志框()

        Dim 参数 = New With {
                .旧客户端路径 = 旧客户端路径框.Text,
                .新客户端路径 = 新客户端路径框.Text,
                .差分包保存路径 = 差分包保存路径框.Text,
                hdiffzExe
            }

        BackgroundWorker2.RunWorkerAsync(参数)
    End Sub

    Private Sub 设置UI状态(是否启用 As Boolean)
        制作选择旧客户端按钮.Enabled = 是否启用
        制作选择新客户端.Enabled = 是否启用
        制作选择差分包.Enabled = 是否启用
        制作按钮.Enabled = 是否启用
    End Sub

    Private Sub BackgroundWorker2_DoWork(sender As Object, e As DoWorkEventArgs)
        Dim 参数 = e.Argument
        Try
            差分包制作器.制作差分包(参数.旧客户端路径, 参数.新客户端路径, 参数.差分包保存路径, 参数.hdiffzExe)
            e.Result = Nothing
        Catch ex As Exception
            e.Result = ex
        End Try
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
        成员_自动调整控件大小?.调整窗体控件大小(Me)
    End Sub
End Class
