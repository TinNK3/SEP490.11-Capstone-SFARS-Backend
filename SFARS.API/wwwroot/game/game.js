'use strict';

// ============ CONFIG ============
const API_BASE = '/api/quizzes';
const LETTERS = ['A', 'B', 'C', 'D'];
const SHOT_POSITIONS = ['28%', '38%', '50%', '62%', '72%']; // x positions for goal shots
const GK_DIVE_POSITIONS = ['28%', '38%', '62%', '72%']; // GK dive points

// ============ STATE ============
let quizData = null;
let currentIndex = 0;
let goalsScored = 0;
let userAnswers = [];
let animating = false;

// ============ DOM ============
const $ = id => document.getElementById(id);
const screens = {
    loader: $('screen-loader'),
    start: $('screen-start'),
    game: $('screen-game'),
    result: $('screen-result'),
};

// ============ UTILS ============
function showScreen(key) {
    Object.values(screens).forEach(s => s.classList.remove('active'));
    screens[key].classList.add('active');
}

function token() {
    return new URLSearchParams(window.location.search).get('token') || localStorage.getItem('jwt_token') || '';
}

function quizId() {
    return new URLSearchParams(window.location.search).get('id');
}

function rand(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
}

// ============ INIT ============
async function init() {
    const id = quizId();
    if (!id) {
        showAlert('⚠️ Thiếu Quiz ID trong URL!');
        return;
    }

    try {
        const headers = { 'Content-Type': 'application/json' };
        if (token()) headers['Authorization'] = `Bearer ${token()}`;

        const res = await fetch(`${API_BASE}/${id}/play`, { headers });
        if (!res.ok) throw new Error(res.status);

        const json = await res.json();
        quizData = json.data;

        $('quiz-title').textContent = quizData.title;
        $('quiz-desc').textContent = quizData.description || 'Trả lời đúng để ghi bàn!';
        $('q-count').textContent = quizData.questions.length;
        $('q-pts').textContent = quizData.pointsReward;

        showScreen('start');
    } catch (err) {
        console.error(err);
        showAlert('❌ Không thể tải Quiz. Kiểm tra ID hoặc kết nối.');
    }
}

function showAlert(msg) {
    $('screen-loader').innerHTML = `<div class="loader-content"><div style="font-size:3rem">⚠️</div><p style="color:#e74c3c;margin-top:15px;font-size:1rem">${msg}</p></div>`;
    showScreen('loader');
}

// ============ GAME LOGIC ============
function startGame() {
    currentIndex = 0;
    goalsScored = 0;
    userAnswers = [];
    $('score-goals').textContent = 0;
    $('score-remain').textContent = quizData.questions.length;
    showScreen('game');
    renderQuestion();
}

function renderQuestion() {
    const q = quizData.questions[currentIndex];
    const total = quizData.questions.length;

    // Progress
    $('q-progress-fill').style.width = `${((currentIndex + 1) / total) * 100}%`;
    $('q-cur').textContent = currentIndex + 1;
    $('q-total').textContent = total;
    $('score-remain').textContent = total - currentIndex;

    // Question text
    $('q-text').textContent = q.content;
    $('q-hint').textContent = q.questionType === 'TrueFalse' ? 'Đúng hay Sai?' : 'Chọn đáp án chính xác';

    // Options
    const container = $('q-options');
    container.innerHTML = '';
    container.style.gridTemplateColumns = q.options.length === 2 ? '1fr 1fr' : '1fr 1fr';

    q.options.forEach((opt, i) => {
        const btn = document.createElement('button');
        btn.className = 'opt-btn';
        btn.dataset.letter = LETTERS[i] || '?';
        btn.dataset.id = opt.id;
        btn.textContent = opt.content;
        btn.onclick = () => handleAnswer(btn, opt.id);
        container.appendChild(btn);
    });

    // Reset ball
    resetBall();
}

function handleAnswer(btn, optId) {
    if (animating) return;
    animating = true;

    // Disable all buttons
    document.querySelectorAll('.opt-btn').forEach(b => b.disabled = true);

    const q = quizData.questions[currentIndex];
    const isCorrect = q.options.find(o => o.id === optId)?.isCorrect !== false
        ? guessCorrect(q, optId)
        : false;

    // Record answer
    const answer = { questionId: q.id, selectedOptionId: optId };
    if (q.questionType === 'Ordering') {
        answer.orderedOptionIds = Array.from(document.querySelectorAll('.opt-btn')).map(b => b.dataset.id);
    }
    userAnswers.push(answer);

    // Highlight correct/wrong
    document.querySelectorAll('.opt-btn').forEach(b => {
        const opt = q.options.find(o => o.id === b.dataset.id);
        if (opt && opt.isCorrect) b.classList.add('correct');
        if (b.dataset.id === optId && !opt?.isCorrect && !isCorrect) b.classList.add('wrong');
    });

    if (isCorrect) {
        animateGoal();
    } else {
        animateSave();
    }
}

// For MCQ: check if selected option is marked correct
function guessCorrect(q, selectedId) {
    const opt = q.options.find(o => o.id === selectedId);
    return opt ? opt.isCorrect !== false : false;
}

// ============ ANIMATIONS ============
function resetBall() {
    const ball = $('ball');
    ball.style.cssText = '';
    ball.style.bottom = '28%';
    ball.style.left = '50%';
    ball.style.fontSize = '2.2rem';
    ball.style.transform = 'translateX(-50%)';
    ball.style.transition = 'none';

    const gk = $('goalkeeper');
    gk.style.cssText = '';
    gk.style.left = '50%';
    gk.style.transform = 'translateX(-50%)';
}

function animateGoal() {
    const ball = $('ball');
    const gk = $('goalkeeper');
    const shotX = rand(SHOT_POSITIONS);
    const gkDecoy = rand(GK_DIVE_POSITIONS.filter(p => p !== shotX)); // GK dives wrong way

    // GK dives WRONG direction (or too slow)
    gk.style.transition = 'left 0.5s ease 0.3s, transform 0.5s ease 0.3s';
    gk.style.left = gkDecoy;
    gk.style.transform = 'translateX(-50%) rotate(' + (gkDecoy > '50%' ? '20deg' : '-20deg') + ')';

    // Ball flies into goal
    ball.style.transition = 'all 0.65s cubic-bezier(0.3, 0.1, 0.2, 1)';
    ball.style.bottom = '42%';
    ball.style.left = shotX;
    ball.style.fontSize = '1rem';
    ball.style.transform = 'translateX(-50%) rotate(720deg)';

    // Flash green
    $('stadium').classList.add('flash-goal');
    setTimeout(() => $('stadium').classList.remove('flash-goal'), 600);

    // Feedback text
    showFeedback('⚽ GOAAAAL!', true);

    goalsScored++;
    $('score-goals').textContent = goalsScored;

    setTimeout(() => {
        hideFeedback();
        nextQuestion();
    }, 1800);
}

function animateSave() {
    const ball = $('ball');
    const gk = $('goalkeeper');
    const shotX = rand(SHOT_POSITIONS);

    // GK dives CORRECT direction to match shot
    gk.style.transition = 'left 0.35s ease, transform 0.35s ease';
    gk.style.left = shotX;
    gk.style.transform = 'translateX(-50%) scale(1.3)';

    // Ball moves but gets caught
    ball.style.transition = 'all 0.45s ease';
    ball.style.bottom = '37%';
    ball.style.left = shotX;
    ball.style.fontSize = '1.6rem';
    ball.style.transform = 'translateX(-50%) rotate(360deg)';

    // Ball bounces back
    setTimeout(() => {
        ball.style.transition = 'all 0.35s ease';
        ball.style.bottom = '28%';
        ball.style.left = '50%';
        ball.style.fontSize = '2.2rem';
        ball.style.transform = 'translateX(-50%)';
    }, 500);

    // Flash red
    $('stadium').classList.add('flash-saved');
    setTimeout(() => $('stadium').classList.remove('flash-saved'), 600);

    // Feedback
    showFeedback('🧤 BỊ BẮT!', false);

    setTimeout(() => {
        hideFeedback();
        nextQuestion();
    }, 1800);
}

function showFeedback(text, isGoal) {
    const overlay = $('feedback-overlay');
    const fText = $('feedback-text');
    fText.textContent = text;
    fText.className = isGoal ? 'feedback-goal' : 'feedback-save';
    overlay.classList.remove('hidden');
}

function hideFeedback() {
    $('feedback-overlay').classList.add('hidden');
}

// ============ NEXT Q / SUBMIT ============
function nextQuestion() {
    currentIndex++;
    animating = false;

    if (currentIndex < quizData.questions.length) {
        renderQuestion();
    } else {
        submitQuiz();
    }
}

async function submitQuiz() {
    showScreen('loader');
    $('screen-loader').innerHTML = '<div class="loader-content"><div class="ball-loader">⚽</div><p>Đang nộp bài...</p></div>';

    try {
        const headers = { 'Content-Type': 'application/json' };
        if (token()) headers['Authorization'] = `Bearer ${token()}`;

        const res = await fetch(`${API_BASE}/submit`, {
            method: 'POST',
            headers,
            body: JSON.stringify({ answers: userAnswers })
        });

        const json = await res.json();
        renderResult(json.data);
    } catch (err) {
        console.error(err);
        // Still show result with local data even if submit fails
        renderResult({
            score: goalsScored,
            totalQuestions: quizData.questions.length,
            pointsEarned: 0,
            message: 'Không thể kết nối máy chủ.'
        });
    }
}

function renderResult(data) {
    const total = data.totalQuestions || quizData.questions.length;
    const goals = data.score ?? goalsScored;
    const missed = total - goals;
    const ratio = goals / total;

    $('res-goals').textContent = goals;
    $('res-missed').textContent = missed;
    $('result-msg').textContent = data.message || (ratio >= 0.8 ? 'Tuyệt vời! Bộ não như máy tính!' : ratio >= 0.5 ? 'Không tệ! Luyện thêm nhé!' : 'Cố lên! Bạn cần ôn thêm.');
    $('result-emoji').textContent = ratio >= 0.8 ? '🏆' : ratio >= 0.5 ? '⚽' : '😅';
    $('result-title').textContent = ratio >= 0.8 ? 'Chiến thắng rực rỡ!' : ratio >= 0.5 ? 'Trận đấu kết thúc!' : 'Thủ môn hôm nay quá mạnh!';

    if (data.pointsEarned > 0) {
        $('result-reward').classList.remove('hidden');
        $('res-pts').textContent = data.pointsEarned;
    } else {
        $('result-reward').classList.add('hidden');
    }

    showScreen('result');
}

// ============ EVENT LISTENERS ============
document.getElementById('btn-start').onclick = startGame;

// ============ BOOT ============
init();
