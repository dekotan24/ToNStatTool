-- 統計データの再計算
-- RoundTypeStats と MapStats を PlayerRound ベースで再計算する

-- ラウンドタイプ統計を再計算
TRUNCATE TABLE round_type_stats;

INSERT INTO round_type_stats (round_type, occurrence_count, total_players, total_survivors, updated_at)
SELECT
    r.round_type,
    COUNT(pr.id) as occurrence_count,
    COUNT(pr.id) as total_players,
    SUM(CASE WHEN pr.survived THEN 1 ELSE 0 END) as total_survivors,
    NOW()
FROM player_rounds pr
JOIN rounds r ON pr.round_id = r.id
GROUP BY r.round_type;

-- マップ統計を再計算
TRUNCATE TABLE map_stats;

INSERT INTO map_stats (map_name, occurrence_count, total_players, total_survivors, updated_at)
SELECT
    r.map_name,
    COUNT(pr.id) as occurrence_count,
    COUNT(pr.id) as total_players,
    SUM(CASE WHEN pr.survived THEN 1 ELSE 0 END) as total_survivors,
    NOW()
FROM player_rounds pr
JOIN rounds r ON pr.round_id = r.id
WHERE r.map_name IS NOT NULL
GROUP BY r.map_name;

-- テラー統計を再計算
TRUNCATE TABLE terror_stats;

INSERT INTO terror_stats (terror_name, encounter_count, total_rounds, total_survivors, updated_at)
SELECT
    terror_name,
    COUNT(*) as encounter_count,
    COUNT(*) as total_rounds,
    SUM(CASE WHEN pr.survived THEN 1 ELSE 0 END) as total_survivors,
    NOW()
FROM player_rounds pr
JOIN rounds r ON pr.round_id = r.id
CROSS JOIN LATERAL unnest(r.terrors) as terror_name
GROUP BY terror_name;
