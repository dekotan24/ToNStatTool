// Internationalization (i18n) System
const translations = {
    ja: {
        // Navigation
        'nav.dashboard': 'ダッシュボード',
        'nav.roundLog': 'ラウンドログ',
        'nav.statistics': '統計',
        'nav.instances': 'インスタンス検索',
        'nav.players': 'プレイヤー検索',
        'nav.settings': '設定',
        'nav.logout': 'ログアウト',

        // Dashboard
        'dashboard.title': 'ダッシュボード',
        'dashboard.myTotalRounds': 'マイラウンド数',
        'dashboard.mySurvivals': '生存数',
        'dashboard.mySurvivalRate': '生存率',
        'dashboard.rounds24h': 'ラウンド数 (24時間)',
        'dashboard.myRecentRounds': '最近のラウンド',
        'dashboard.instanceHistory': 'インスタンス履歴',
        'dashboard.viewMyHistory': 'マイ履歴を見る',
        'dashboard.myRounds': 'マイラウンド',
        'dashboard.noRoundsYet': 'まだラウンドがありません。C#アプリでプレイを開始してください。',
        'dashboard.noInstanceHistory': 'インスタンス履歴がありません',

        // Common table headers
        'table.time': '時間',
        'table.roundType': 'ラウンド種別',
        'table.map': 'マップ',
        'table.terrors': 'テラー',
        'table.players': 'プレイヤー数',
        'table.instance': 'インスタンス',
        'table.instanceId': 'インスタンスID',
        'table.lastActivity': '最終アクティビティ',
        'table.rounds': 'ラウンド数',
        'table.totalRounds': '総ラウンド数',
        'table.result': '結果',
        'table.items': 'アイテム',
        'table.played': 'プレイ数',
        'table.survived': '生存数',
        'table.rate': '生存率',
        'table.encounters': '遭遇数',
        'table.survivals': '生存数',
        'table.survivalRate': '生存率',
        'table.timesHeld': '所持回数',

        // Round Log
        'roundLog.title': 'ラウンドログ',
        'roundLog.filterRoundType': 'ラウンド種別',
        'roundLog.filterTerror': 'テラー',
        'roundLog.filterMap': 'マップ',
        'roundLog.allTypes': '全ての種別',
        'roundLog.filterByTerror': 'テラー名で絞り込み...',
        'roundLog.filterByMap': 'マップで絞り込み...',
        'roundLog.allMaps': '全てのマップ',

        // Instances
        'instances.title': 'インスタンス検索',
        'instances.instanceId': 'インスタンスID',
        'instances.search': '検索',
        'instances.clear': 'クリア',
        'instances.allMaps': '全てのマップ',
        'instances.searchTerror': 'テラー名を検索...',
        'instances.resultsFound': '件のインスタンスが見つかりました',
        'instances.enterCriteria': '検索条件を入力して検索ボタンをクリック',

        // Instance Detail
        'instanceDetail.title': 'インスタンス詳細',
        'instanceDetail.backToInstances': 'インスタンス一覧に戻る',
        'instanceDetail.totalRounds': '総ラウンド数',
        'instanceDetail.created': '作成日時',
        'instanceDetail.lastActivity': '最終アクティビティ',
        'instanceDetail.joinInstance': 'インスタンスに参加',
        'instanceDetail.roundTypes': 'ラウンド種別',
        'instanceDetail.maps': 'マップ',
        'instanceDetail.terrors': 'テラー',
        'instanceDetail.roundHistory': 'ラウンド履歴',
        'instanceDetail.last50': '直近50件',
        'instanceDetail.noData': 'データなし',

        // Statistics
        'statistics.title': '統計',
        'statistics.roundTypes': 'ラウンド種別統計',
        'statistics.maps': 'マップ統計',
        'statistics.terrors': 'テラー統計',
        'statistics.occurrence': '発生回数',
        'statistics.percentage': '割合',
        'statistics.encounterCount': '遭遇数',

        // Players
        'players.title': 'プレイヤー検索',
        'players.subtitle': 'プレイヤーのプロフィールを検索',
        'players.searchPlaceholder': 'VRChat名で検索...',
        'players.search': '検索',
        'players.playersFound': '人のプレイヤーが見つかりました',
        'players.enterName': 'プレイヤー名を入力して検索',
        'players.globalItemStats': 'グローバルアイテム統計',
        'players.itemStatsSubtitle': '特定アイテム所持時の生存率',
        'players.noPlayers': 'プレイヤーが見つかりません',

        // Profile
        'profile.private': '非公開',
        'profile.yourProfile': '自分のプロフィール',
        'profile.rounds': 'ラウンド',
        'profile.survivals': '生存',
        'profile.survivalRate': '生存率',
        'profile.myDetailedLog': '詳細ログを見る',
        'profile.overview': '概要',
        'profile.history': 'ラウンド履歴',
        'profile.items': 'アイテム',
        'profile.terrors': 'テラー',
        'profile.roundTypeStats': 'ラウンド種別統計',
        'profile.loadMore': 'もっと読み込む',
        'profile.survived': '生存',
        'profile.died': '死亡',
        'profile.notFound': 'プレイヤーが見つかりません',
        'profile.isPrivate': 'このプロフィールは非公開です',
        'profile.noData': 'データがありません',

        // Settings
        'settings.title': '設定',
        'settings.apiKeys': 'APIキー',
        'settings.security': 'セキュリティ',
        'settings.profile': 'プロフィール',
        'settings.createApiKey': 'APIキーを作成',
        'settings.apiKeysDesc': 'C#アプリとの連携に使用するAPIキーを作成します。',
        'settings.keyName': 'キー名',
        'settings.create': '作成',
        'settings.important': '重要:',
        'settings.keyOnlyOnce': 'このキーは一度だけ表示されます。安全に保管してください。',
        'settings.copy': 'コピー',
        'settings.copied': 'コピー完了!',
        'settings.yourApiKeys': 'あなたのAPIキー',
        'settings.name': '名前',
        'settings.keyPrefix': 'キー接頭辞',
        'settings.status': '状態',
        'settings.statusActive': '有効',
        'settings.statusRevoked': '無効',
        'settings.lastUsed': '最終使用',
        'settings.createdAt': '作成日',
        'settings.actions': '操作',
        'settings.revoke': '無効化',
        'settings.noApiKeys': 'APIキーがありません',
        'settings.setupGuide': 'セットアップガイド',
        'settings.step1': '上の「APIキーを作成」でキー名を入力して作成をクリック',
        'settings.step2': '表示されたAPIキー（ton_で始まる文字列）をコピー',
        'settings.step3': 'C#アプリを開き、設定画面へ移動',
        'settings.step4': 'クラウド同期を有効化にチェック',
        'settings.step5': 'サーバーURLに',
        'settings.step5b': 'を入力',
        'settings.step6': 'APIキーにコピーしたキーを貼り付け',
        'settings.step7': '保存をクリック',
        'settings.keyNote': 'APIキーは一度だけ表示されます。紛失した場合は新しいキーを作成してください。',
        'settings.changePassword': 'パスワード変更',
        'settings.currentPassword': '現在のパスワード',
        'settings.newPassword': '新しいパスワード',
        'settings.confirmNewPassword': '新しいパスワード（確認）',
        'settings.minChars': '8文字以上',
        'settings.changePasswordBtn': 'パスワードを変更',
        'settings.twoFactorAuth': '二要素認証 (2FA)',
        'settings.2faEnabled': '2FAは有効です',
        'settings.2faDescription': '2FAを有効にしてアカウントを保護しましょう。',
        'settings.setup2fa': '2FAをセットアップ',
        'settings.scanQrCode': '認証アプリでQRコードをスキャンしてください。',
        'settings.secretKey': 'Secret Key (手動入力用):',
        'settings.backupCodes': 'バックアップコード (安全に保管):',
        'settings.enterAuthCode': '認証コードを入力',
        'settings.verifyAndEnable': '確認して有効化',
        'settings.cancel': 'キャンセル',
        'settings.enterCodeToDisable': '認証コードを入力して無効化',
        'settings.disable2fa': '2FAを無効化',
        'settings.playerProfileSettings': 'プレイヤープロフィール設定',
        'settings.makeProfilePublic': 'プロフィールを公開する',
        'settings.publicProfileHint': 'オフにすると、プレイヤー検索に表示されなくなります。',
        'settings.saveChanges': '変更を保存',
        'settings.noPlayerProfile': 'プレイヤープロフィールがまだありません。C#アプリでラウンドデータを送信すると自動的に作成されます。',
        'settings.accountInfo': 'アカウント情報',
        'settings.username': 'ユーザー名:',
        'settings.email': 'メールアドレス:',
        'settings.createFailed': 'APIキーの作成に失敗しました',
        'settings.revokeConfirm': 'このAPIキーを無効化しますか？',
        'settings.revokeFailed': 'APIキーの無効化に失敗しました',
        'settings.passwordMismatch': '新しいパスワードが一致しません',
        'settings.changePasswordFailed': 'パスワードの変更に失敗しました',
        'settings.passwordChanged': 'パスワードが変更されました',
        'settings.setup2faFailed': '2FAのセットアップに失敗しました',
        'settings.invalidCode': '認証コードが無効です',
        'settings.2faEnabledSuccess': '2FAが有効になりました',
        'settings.verify2faFailed': '2FAの確認に失敗しました',
        'settings.disable2faConfirm': '2FAを無効化しますか？',
        'settings.2faDisabled': '2FAが無効になりました',
        'settings.disable2faFailed': '2FAの無効化に失敗しました',
        'settings.saveFailed': '設定の保存に失敗しました',
        'settings.saved': '設定を保存しました',

        // Login/Register
        'login.title': 'ログイン',
        'login.username': 'ユーザー名',
        'login.password': 'パスワード',
        'login.submit': 'ログイン',
        'login.loggingIn': 'ログイン中...',
        'login.noAccount': 'アカウントをお持ちでない方は',
        'login.register': '新規登録',
        'login.completeVerification': '認証を完了してください',
        'login.failed': 'ログインに失敗しました',
        'register.title': '新規登録',
        'register.email': 'メールアドレス',
        'register.confirmPassword': 'パスワード確認',
        'register.usernameHint': '3-50文字、英数字と_-のみ',
        'register.passwordHint': '8文字以上',
        'register.submit': '登録',
        'register.creating': 'アカウント作成中...',
        'register.hasAccount': 'すでにアカウントをお持ちの方は',
        'register.login': 'ログイン',
        'register.passwordMismatch': 'パスワードが一致しません',
        'register.completeVerification': '認証を完了してください',
        'register.failed': '登録に失敗しました',

        // Common
        'common.loading': '読み込み中...',
        'common.searching': '検索中...',
        'common.error': 'エラーが発生しました',
        'common.connectionError': '接続エラー。再度お試しください。',
        'common.noResults': '結果がありません',
        'common.previous': '前へ',
        'common.next': '次へ',
        'common.page': 'ページ',
        'common.of': '/',
        'common.searchFailed': '検索に失敗しました',
        'common.failedToLoad': '読み込みに失敗しました',
        'common.noRoundsYet': 'まだラウンドが記録されていません',
        'common.noActiveInstances': 'アクティブなインスタンスがありません',
        'common.noDataYet': 'データがありません',
        'common.noRoundsFound': 'ラウンドが見つかりません',
        'common.noTerrors': 'テラーが見つかりません',

        // My History
        'myHistory.title': 'マイラウンド履歴',
        'myHistory.subtitle': 'アイテムと生存データを含む個人のラウンドログ',
        'myHistory.filterSurvival': '生存状態',
        'myHistory.all': '全て',
        'myHistory.survived': '生存',
        'myHistory.died': '死亡',
        'myHistory.filterItem': 'アイテム',
        'myHistory.filterByItem': 'アイテムで絞り込み...',
        'myHistory.totalRounds': '総ラウンド数',
        'myHistory.survivals': '生存数',
        'myHistory.survivalRate': '生存率',
        'myHistory.uniqueItems': 'ユニークアイテム',
        'myHistory.itemsHeld': '所持アイテム',
        'myHistory.profileSettings': 'プロフィール設定',
        'myHistory.bio': '自己紹介',
        'myHistory.bioPlaceholder': '自己紹介を書いてください...',
        'myHistory.maxChars': '最大500文字',
        'myHistory.makePublic': 'プロフィールを公開（他のユーザーがあなたの統計を閲覧可能）',
        'myHistory.saveSettings': '設定を保存',
        'myHistory.publicProfile': '公開プロフィール',
        'myHistory.privateProfile': '非公開プロフィール',
        'myHistory.viewPublicProfile': '公開プロフィールを見る',
        'myHistory.noProfile': 'プレイヤープロフィールがまだありません。C#アプリでラウンドをプレイして作成してください！',
        'myHistory.noProfileHint': 'C#アプリがVRChat名でプレイヤーデータを送信していることを確認してください。',
        'myHistory.noMatchingRounds': '条件に一致するラウンドがありません',
        'myHistory.noProfileFound': 'プロフィールが見つかりません',

        // Instance Detail
        'instanceDetail.noRoundsRecorded': 'ラウンドが記録されていません',

        // Players
        'players.enterNamePrompt': 'プレイヤー名を入力して検索',
        'players.noItemData': 'アイテムデータがありません',

        // Instances
        'instances.noInstancesFound': 'インスタンスが見つかりません',

        // Index/Landing
        'index.welcome': 'ようこそ',
        'index.subtitle': 'Terror of Nowhere 統計ダッシュボード',
        'index.goToDashboard': 'ダッシュボードへ',

        // Global Statistics
        'globalStats.title': 'グローバル統計',
        'globalStats.roundTypes': 'ラウンド種別統計',
        'globalStats.maps': 'マップ統計',
        'globalStats.terrors': 'テラー統計',
        'globalStats.items': 'アイテム統計',
        'globalStats.occurrence': '発生回数',
        'globalStats.percentage': '割合',
        'globalStats.encounters': '遭遇数',
        'globalStats.survivalRate': '生存率',
        'globalStats.timesHeld': '所持回数',
        'globalStats.survivals': '生存数',

        // My Statistics (for my-history page)
        'myStats.roundTypes': 'マイラウンド種別統計',
        'myStats.maps': 'マイマップ統計',
        'myStats.terrors': 'マイテラー統計',
        'myStats.played': 'プレイ数',
        'myStats.survived': '生存数',
        'myStats.survivalRate': '生存率',
    },
    en: {
        // Navigation
        'nav.dashboard': 'Dashboard',
        'nav.roundLog': 'Round Log',
        'nav.statistics': 'Statistics',
        'nav.instances': 'Instances',
        'nav.players': 'Players',
        'nav.settings': 'Settings',
        'nav.logout': 'Logout',

        // Dashboard
        'dashboard.title': 'Dashboard',
        'dashboard.myTotalRounds': 'My Rounds',
        'dashboard.mySurvivals': 'Survivals',
        'dashboard.mySurvivalRate': 'Survival Rate',
        'dashboard.rounds24h': 'Rounds (24h)',
        'dashboard.myRecentRounds': 'Recent Rounds',
        'dashboard.instanceHistory': 'Instance History',
        'dashboard.viewMyHistory': 'View My History',
        'dashboard.myRounds': 'My Rounds',
        'dashboard.noRoundsYet': 'No rounds yet. Start playing with the C# app!',
        'dashboard.noInstanceHistory': 'No instance history',

        // Common table headers
        'table.time': 'Time',
        'table.roundType': 'Round Type',
        'table.map': 'Map',
        'table.terrors': 'Terrors',
        'table.players': 'Players',
        'table.instance': 'Instance',
        'table.instanceId': 'Instance ID',
        'table.lastActivity': 'Last Activity',
        'table.rounds': 'Rounds',
        'table.totalRounds': 'Total Rounds',
        'table.result': 'Result',
        'table.items': 'Items',
        'table.played': 'Played',
        'table.survived': 'Survived',
        'table.rate': 'Rate',
        'table.encounters': 'Encounters',
        'table.survivals': 'Survivals',
        'table.survivalRate': 'Survival Rate',
        'table.timesHeld': 'Times Held',

        // Round Log
        'roundLog.title': 'Round Log',
        'roundLog.filterRoundType': 'Round Type',
        'roundLog.filterTerror': 'Terror',
        'roundLog.filterMap': 'Map',
        'roundLog.allTypes': 'All Types',
        'roundLog.filterByTerror': 'Filter by terror name...',
        'roundLog.filterByMap': 'Filter by map...',
        'roundLog.allMaps': 'All Maps',

        // Instances
        'instances.title': 'Instance Search',
        'instances.instanceId': 'Instance ID',
        'instances.search': 'Search',
        'instances.clear': 'Clear',
        'instances.allMaps': 'All Maps',
        'instances.searchTerror': 'Search terror name...',
        'instances.resultsFound': 'instance(s) found',
        'instances.enterCriteria': 'Enter search criteria and click Search',

        // Instance Detail
        'instanceDetail.title': 'Instance Detail',
        'instanceDetail.backToInstances': 'Back to Instances',
        'instanceDetail.totalRounds': 'Total Rounds',
        'instanceDetail.created': 'Created',
        'instanceDetail.lastActivity': 'Last Activity',
        'instanceDetail.joinInstance': 'Join Instance',
        'instanceDetail.roundTypes': 'Round Types',
        'instanceDetail.maps': 'Maps',
        'instanceDetail.terrors': 'Terrors',
        'instanceDetail.roundHistory': 'Round History',
        'instanceDetail.last50': 'Last 50',
        'instanceDetail.noData': 'No data',

        // Statistics
        'statistics.title': 'Statistics',
        'statistics.roundTypes': 'Round Type Statistics',
        'statistics.maps': 'Map Statistics',
        'statistics.terrors': 'Terror Statistics',
        'statistics.occurrence': 'Occurrence',
        'statistics.percentage': 'Percentage',
        'statistics.encounterCount': 'Encounter Count',

        // Players
        'players.title': 'Player Search',
        'players.subtitle': 'Find and view player profiles',
        'players.searchPlaceholder': 'Search by VRChat name...',
        'players.search': 'Search',
        'players.playersFound': 'player(s) found',
        'players.enterName': 'Enter a name to search for players',
        'players.globalItemStats': 'Global Item Statistics',
        'players.itemStatsSubtitle': 'Survival rates when holding specific items',
        'players.noPlayers': 'No players found',

        // Profile
        'profile.private': 'Private',
        'profile.yourProfile': 'Your Profile',
        'profile.rounds': 'Rounds',
        'profile.survivals': 'Survivals',
        'profile.survivalRate': 'Survival Rate',
        'profile.myDetailedLog': 'My Detailed Log',
        'profile.overview': 'Overview',
        'profile.history': 'Round History',
        'profile.items': 'Items',
        'profile.terrors': 'Terrors',
        'profile.roundTypeStats': 'Round Type Stats',
        'profile.loadMore': 'Load More',
        'profile.survived': 'Survived',
        'profile.died': 'Died',
        'profile.notFound': 'Player not found',
        'profile.isPrivate': 'This profile is private',
        'profile.noData': 'No data yet',

        // Settings
        'settings.title': 'Settings',
        'settings.apiKeys': 'API Keys',
        'settings.security': 'Security',
        'settings.profile': 'Profile',
        'settings.createApiKey': 'Create API Key',
        'settings.apiKeysDesc': 'Create API keys for C# app integration.',
        'settings.keyName': 'Key Name',
        'settings.create': 'Create',
        'settings.important': 'Important:',
        'settings.keyOnlyOnce': 'This key will only be displayed once. Store it securely.',
        'settings.copy': 'Copy',
        'settings.copied': 'Copied!',
        'settings.yourApiKeys': 'Your API Keys',
        'settings.name': 'Name',
        'settings.keyPrefix': 'Key Prefix',
        'settings.status': 'Status',
        'settings.statusActive': 'Active',
        'settings.statusRevoked': 'Revoked',
        'settings.lastUsed': 'Last Used',
        'settings.createdAt': 'Created',
        'settings.actions': 'Actions',
        'settings.revoke': 'Revoke',
        'settings.noApiKeys': 'No API keys',
        'settings.setupGuide': 'Setup Guide',
        'settings.step1': 'Enter a key name above and click Create',
        'settings.step2': 'Copy the displayed API key (starts with ton_)',
        'settings.step3': 'Open the C# app and go to Settings',
        'settings.step4': 'Enable Cloud Sync',
        'settings.step5': 'Enter the Server URL:',
        'settings.step5b': '',
        'settings.step6': 'Paste the copied API key',
        'settings.step7': 'Click Save',
        'settings.keyNote': 'API keys are only displayed once. If lost, create a new key.',
        'settings.changePassword': 'Change Password',
        'settings.currentPassword': 'Current Password',
        'settings.newPassword': 'New Password',
        'settings.confirmNewPassword': 'Confirm New Password',
        'settings.minChars': 'Minimum 8 characters',
        'settings.changePasswordBtn': 'Change Password',
        'settings.twoFactorAuth': 'Two-Factor Authentication (2FA)',
        'settings.2faEnabled': '2FA is enabled',
        'settings.2faDescription': 'Enable 2FA to protect your account.',
        'settings.setup2fa': 'Setup 2FA',
        'settings.scanQrCode': 'Scan the QR code with your authenticator app.',
        'settings.secretKey': 'Secret Key (for manual entry):',
        'settings.backupCodes': 'Backup Codes (store securely):',
        'settings.enterAuthCode': 'Enter authentication code',
        'settings.verifyAndEnable': 'Verify & Enable',
        'settings.cancel': 'Cancel',
        'settings.enterCodeToDisable': 'Enter code to disable',
        'settings.disable2fa': 'Disable 2FA',
        'settings.playerProfileSettings': 'Player Profile Settings',
        'settings.makeProfilePublic': 'Make profile public',
        'settings.publicProfileHint': 'When off, your profile will not appear in player search.',
        'settings.saveChanges': 'Save Changes',
        'settings.noPlayerProfile': 'No player profile yet. Send round data from the C# app to create one.',
        'settings.accountInfo': 'Account Info',
        'settings.username': 'Username:',
        'settings.email': 'Email:',
        'settings.createFailed': 'Failed to create API key',
        'settings.revokeConfirm': 'Revoke this API key?',
        'settings.revokeFailed': 'Failed to revoke API key',
        'settings.passwordMismatch': 'New passwords do not match',
        'settings.changePasswordFailed': 'Failed to change password',
        'settings.passwordChanged': 'Password changed successfully',
        'settings.setup2faFailed': 'Failed to setup 2FA',
        'settings.invalidCode': 'Invalid code',
        'settings.2faEnabledSuccess': '2FA has been enabled',
        'settings.verify2faFailed': 'Failed to verify 2FA',
        'settings.disable2faConfirm': 'Disable 2FA?',
        'settings.2faDisabled': '2FA has been disabled',
        'settings.disable2faFailed': 'Failed to disable 2FA',
        'settings.saveFailed': 'Failed to save settings',
        'settings.saved': 'Settings saved',

        // Login/Register
        'login.title': 'Login',
        'login.username': 'Username',
        'login.password': 'Password',
        'login.submit': 'Login',
        'login.loggingIn': 'Logging in...',
        'login.noAccount': "Don't have an account?",
        'login.register': 'Register',
        'login.completeVerification': 'Please complete the verification',
        'login.failed': 'Login failed',
        'register.title': 'Register',
        'register.email': 'Email',
        'register.confirmPassword': 'Confirm Password',
        'register.usernameHint': '3-50 characters, letters, numbers, _ and - only',
        'register.passwordHint': 'Minimum 8 characters',
        'register.submit': 'Register',
        'register.creating': 'Creating account...',
        'register.hasAccount': 'Already have an account?',
        'register.login': 'Login',
        'register.passwordMismatch': 'Passwords do not match',
        'register.completeVerification': 'Please complete the verification',
        'register.failed': 'Registration failed',

        // Common
        'common.loading': 'Loading...',
        'common.searching': 'Searching...',
        'common.error': 'An error occurred',
        'common.connectionError': 'Connection error. Please try again.',
        'common.noResults': 'No results',
        'common.previous': 'Previous',
        'common.next': 'Next',
        'common.page': 'Page',
        'common.of': 'of',
        'common.searchFailed': 'Search failed',
        'common.failedToLoad': 'Failed to load',
        'common.noRoundsYet': 'No rounds recorded yet',
        'common.noActiveInstances': 'No active instances',
        'common.noDataYet': 'No data yet',
        'common.noRoundsFound': 'No rounds found',
        'common.noTerrors': 'No terrors found',

        // My History
        'myHistory.title': 'My Round History',
        'myHistory.subtitle': 'Your personal round log with detailed item and survival data',
        'myHistory.filterSurvival': 'Survival',
        'myHistory.all': 'All',
        'myHistory.survived': 'Survived',
        'myHistory.died': 'Died',
        'myHistory.filterItem': 'Item',
        'myHistory.filterByItem': 'Filter by item...',
        'myHistory.totalRounds': 'Total Rounds',
        'myHistory.survivals': 'Survivals',
        'myHistory.survivalRate': 'Survival Rate',
        'myHistory.uniqueItems': 'Unique Items',
        'myHistory.itemsHeld': 'Items Held',
        'myHistory.profileSettings': 'Profile Settings',
        'myHistory.bio': 'Bio',
        'myHistory.bioPlaceholder': 'Write something about yourself...',
        'myHistory.maxChars': 'Max 500 characters',
        'myHistory.makePublic': 'Make profile public (others can view your stats)',
        'myHistory.saveSettings': 'Save Settings',
        'myHistory.publicProfile': 'Public Profile',
        'myHistory.privateProfile': 'Private Profile',
        'myHistory.viewPublicProfile': 'View Public Profile',
        'myHistory.noProfile': "You don't have a player profile yet. Play some rounds with the C# app to create one!",
        'myHistory.noProfileHint': 'Make sure your C# app is sending player data with your VRChat name.',
        'myHistory.noMatchingRounds': 'No matching rounds found',
        'myHistory.noProfileFound': 'No profile found',

        // Instance Detail
        'instanceDetail.noRoundsRecorded': 'No rounds recorded',

        // Players
        'players.enterNamePrompt': 'Enter a name to search for players',
        'players.noItemData': 'No item data yet',

        // Instances
        'instances.noInstancesFound': 'No instances found',

        // Index/Landing
        'index.welcome': 'Welcome',
        'index.subtitle': 'Terror of Nowhere Statistics Dashboard',
        'index.goToDashboard': 'Go to Dashboard',

        // Global Statistics
        'globalStats.title': 'Global Statistics',
        'globalStats.roundTypes': 'Round Type Statistics',
        'globalStats.maps': 'Map Statistics',
        'globalStats.terrors': 'Terror Statistics',
        'globalStats.items': 'Item Statistics',
        'globalStats.occurrence': 'Occurrence',
        'globalStats.percentage': 'Percentage',
        'globalStats.encounters': 'Encounters',
        'globalStats.survivalRate': 'Survival Rate',
        'globalStats.timesHeld': 'Times Held',
        'globalStats.survivals': 'Survivals',

        // My Statistics (for my-history page)
        'myStats.roundTypes': 'My Round Type Stats',
        'myStats.maps': 'My Map Stats',
        'myStats.terrors': 'My Terror Stats',
        'myStats.played': 'Played',
        'myStats.survived': 'Survived',
        'myStats.survivalRate': 'Survival Rate',
    }
};

// Get current language from localStorage or default to Japanese
function getCurrentLanguage() {
    return localStorage.getItem('language') || 'ja';
}

// Set language
function setLanguage(lang) {
    localStorage.setItem('language', lang);
    applyTranslations();
    // Update language selector if exists
    const selector = document.getElementById('languageSelector');
    if (selector) selector.value = lang;
}

// Get translation
function t(key) {
    const lang = getCurrentLanguage();
    return translations[lang]?.[key] || translations['en']?.[key] || key;
}

// Apply translations to all elements with data-i18n attribute
function applyTranslations() {
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        const text = t(key);
        if (el.tagName === 'INPUT' && el.placeholder !== undefined) {
            el.placeholder = text;
        } else {
            el.textContent = text;
        }
    });

    // Update document title if has data-i18n-title
    const titleEl = document.querySelector('[data-i18n-title]');
    if (titleEl) {
        document.title = t(titleEl.getAttribute('data-i18n-title')) + ' - ToN Stat Web';
    }
}

// Initialize on DOM load
document.addEventListener('DOMContentLoaded', () => {
    applyTranslations();

    // Setup language selector if exists
    const selector = document.getElementById('languageSelector');
    if (selector) {
        selector.value = getCurrentLanguage();
        selector.addEventListener('change', (e) => setLanguage(e.target.value));
    }
});
