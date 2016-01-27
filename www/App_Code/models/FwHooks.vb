' Fw Hooks class
' global framework hooks can be set here
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2015 Oleg Savchuk www.osalabs.com

Public Class FwHooks

    'called from FW.run before request dispatch
    Public Shared Sub request_init(fw As FW)
        'Dim main_menu As ArrayList = FwCache.get_value("main_menu")

        'If IsNothing(main_menu) OrElse main_menu.Count = 0 Then
        '    'create main menu if not yet
        '    main_menu = fw.model(Of Settings).get_main_menu()
        '    FwCache.set_value("main_menu", main_menu)
        'End If

        'fw.G("main_menu") = main_menu

        'also force set XSS
        If Not fw.SESSION("XSS") > "" Then fw.SESSION("XSS", Utils.get_rand_str(16))
    End Sub

End Class
