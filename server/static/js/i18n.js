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
        'dashboard.totalRounds': '総ラウンド数',
        'dashboard.totalInstances': '総インスタンス数',
        'dashboard.terrorsEncountered': 'テラー遭遇数',
        'dashboard.rounds24h': 'ラウンド数 (24時間)',
        'dashboard.recentRounds': '最近のラウンド',
        'dashboard.activeInstances': 'アクティブインスタンス',
        'dashboard.viewAllRounds': '全てのラウンドを表示',

        // Common table headers
        'table.time': '時間',
        'table.roundType': 'ラウンド種別',
        'table.map': 'マップ',
        'table.terrors': 'テラー',
        'table.players': 'プレイヤー数',
        'table.instance': 'インスタンス',
        'table.lastActivity': '最終アクティビティ',
        'table.rounds': 'ラウンド数',
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
        'settings.apiKeysDesc': 'C#アプリとの連携用APIキー',
        'settings.createKey': 'キーを作成',
        'settings.keyName': 'キー名',
        'settings.createdAt': '作成日',
        'settings.actions': '操作',
        'settings.delete': '削除',
        'settings.noKeys': 'APIキーがありません',
        'settings.setupInstructions': 'セットアップ手順',
        'settings.step1': 'ステップ1: 上の「キーを作成」でAPIキーを作成',
        'settings.step2': 'ステップ2: 作成されたキーをコピー',
        'settings.step3': 'ステップ3: C#アプリの設定画面でキーを入力',
        'settings.profileSettings': 'プロフィール設定',
        'settings.publicProfile': 'プロフィールを公開',
        'settings.publicProfileDesc': '他のユーザーがあなたのプロフィールを閲覧できます',
        'settings.save': '保存',

        // Login/Register
        'login.title': 'ログイン',
        'login.username': 'ユーザー名',
        'login.password': 'パスワード',
        'login.submit': 'ログイン',
        'login.noAccount': 'アカウントをお持ちでない方は',
        'login.register': '新規登録',
        'register.title': '新規登録',
        'register.email': 'メールアドレス',
        'register.confirmPassword': 'パスワード確認',
        'register.submit': '登録',
        'register.hasAccount': 'すでにアカウントをお持ちの方は',
        'register.login': 'ログイン',

        // Common
        'common.loading': '読み込み中...',
        'common.error': 'エラーが発生しました',
        'common.noResults': '結果がありません',
        'common.previous': '前へ',
        'common.next': '次へ',
        'common.page': 'ページ',
        'common.of': '/',
        'common.searchFailed': '検索に失敗しました',
        'common.noRoundsYet': 'まだラウンドが記録されていません',
        'common.noActiveInstances': 'アクティブなインスタンスがありません',
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
        'dashboard.totalRounds': 'Total Rounds',
        'dashboard.totalInstances': 'Total Instances',
        'dashboard.terrorsEncountered': 'Terrors Encountered',
        'dashboard.rounds24h': 'Rounds (24h)',
        'dashboard.recentRounds': 'Recent Rounds',
        'dashboard.activeInstances': 'Active Instances',
        'dashboard.viewAllRounds': 'View All Rounds',

        // Common table headers
        'table.time': 'Time',
        'table.roundType': 'Round Type',
        'table.map': 'Map',
        'table.terrors': 'Terrors',
        'table.players': 'Players',
        'table.instance': 'Instance',
        'table.lastActivity': 'Last Activity',
        'table.rounds': 'Rounds',
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
        'settings.apiKeysDesc': 'API keys for C# app integration',
        'settings.createKey': 'Create Key',
        'settings.keyName': 'Key Name',
        'settings.createdAt': 'Created',
        'settings.actions': 'Actions',
        'settings.delete': 'Delete',
        'settings.noKeys': 'No API keys',
        'settings.setupInstructions': 'Setup Instructions',
        'settings.step1': 'Step 1: Create an API key using "Create Key" above',
        'settings.step2': 'Step 2: Copy the generated key',
        'settings.step3': 'Step 3: Enter the key in the C# app settings',
        'settings.profileSettings': 'Profile Settings',
        'settings.publicProfile': 'Public Profile',
        'settings.publicProfileDesc': 'Other users can view your profile',
        'settings.save': 'Save',

        // Login/Register
        'login.title': 'Login',
        'login.username': 'Username',
        'login.password': 'Password',
        'login.submit': 'Login',
        'login.noAccount': "Don't have an account?",
        'login.register': 'Register',
        'register.title': 'Register',
        'register.email': 'Email',
        'register.confirmPassword': 'Confirm Password',
        'register.submit': 'Register',
        'register.hasAccount': 'Already have an account?',
        'register.login': 'Login',

        // Common
        'common.loading': 'Loading...',
        'common.error': 'An error occurred',
        'common.noResults': 'No results',
        'common.previous': 'Previous',
        'common.next': 'Next',
        'common.page': 'Page',
        'common.of': 'of',
        'common.searchFailed': 'Search failed',
        'common.noRoundsYet': 'No rounds recorded yet',
        'common.noActiveInstances': 'No active instances',
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
        document.title = t(titleEl.getAttribute('data-i18n-title')) + ' - ToN Stats';
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
