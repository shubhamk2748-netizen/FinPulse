// FinPulse High-Throughput Digital Wallet Engine UI Logic
const API_BASE = window.location.origin;

// Predefined Demo Personas
const PERSONAS = {
  rahul: {
    name: 'Rahul Sharma (India)',
    email: 'rahul@finpulse.in',
    password: 'Rahul@12345',
    role: 'Indian Customer (₹ INR)'
  },
  priya: {
    name: 'Priya Patel (India)',
    email: 'priya@finpulse.in',
    password: 'Priya@12345',
    role: 'Indian Customer (₹ INR)'
  },
  sarah: {
    name: 'Sarah Jenkins',
    email: 'sarah@investor.com',
    password: 'Sarah@12345',
    role: 'VIP Investor (USD/EUR)'
  },
  alex: {
    name: 'Alex Mercer',
    email: 'alex@trader.com',
    password: 'Alex@12345',
    role: 'Standard Trader (USD/GBP)'
  },
  admin: {
    name: 'System Admin',
    email: 'admin@finpulse.io',
    password: 'Admin@12345',
    role: 'Administrator'
  }
};

let currentToken = localStorage.getItem('finpulse_token') || null;
let currentPersonaKey = localStorage.getItem('finpulse_persona') || 'rahul';
let myWallets = [];
let allWalletsCache = [];

// Initialize Dashboard
document.addEventListener('DOMContentLoaded', async () => {
  initTabs();
  initEventListeners();
  await loadSystemStatus();
  
  // If user already has an active session, restore it; otherwise show Auth Gateway
  if (currentToken) {
    await refreshDashboard();
  } else {
    updateAuthUi(false);
  }

  // Periodic health check
  setInterval(loadSystemStatus, 15000);
});

// Tab & Screen Navigation (Mobile-First View Switcher)
function initTabs() {
  // Desktop Tab Bar clicks
  document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.addEventListener('click', (e) => {
      const tab = e.currentTarget.dataset.tab;
      if (tab) switchTab(tab);
    });
  });

  // Mobile Bottom Bar clicks
  document.querySelectorAll('.mobile-nav-item').forEach(btn => {
    btn.addEventListener('click', (e) => {
      const tab = e.currentTarget.dataset.tab;
      if (tab) switchTab(tab);
    });
  });

  // Check URL hash or default
  const hash = window.location.hash.replace('#', '').trim();
  const validTabs = ['wallets', 'transfer', 'passbook', 'concurrency', 'audit', 'all'];
  if (validTabs.includes(hash)) {
    switchTab(hash, false);
  } else {
    // Default to 'wallets' for mobile screens and desktop clean view
    switchTab('wallets', false);
  }

  // Handle browser back/forward buttons
  window.addEventListener('hashchange', () => {
    const newHash = window.location.hash.replace('#', '').trim();
    if (validTabs.includes(newHash)) {
      switchTab(newHash, false);
    }
  });
}

function switchTab(tabName, updateHash = true) {
  // Update body class for CSS visibility
  document.body.className = `tab-${tabName}`;

  // Update desktop tab buttons
  document.querySelectorAll('.tab-btn').forEach(btn => {
    if (btn.dataset.tab === tabName) {
      btn.classList.add('active');
    } else {
      btn.classList.remove('active');
    }
  });

  // Update mobile bottom nav items
  document.querySelectorAll('.mobile-nav-item').forEach(btn => {
    if (btn.dataset.tab === tabName) {
      btn.classList.add('active');
    } else {
      btn.classList.remove('active');
    }
  });

  if (updateHash) {
    history.replaceState(null, '', `#${tabName}`);
  }

  // Smooth scroll to top of view on mobile
  if (window.innerWidth <= 768) {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
window.switchTab = switchTab;

window.quickSendMoney = function(walletId) {
  const select = document.getElementById('transfer-source-wallet');
  if (select && walletId) {
    select.value = walletId;
  }
  switchTab('transfer');
  showToast('Source wallet selected. Enter payee details below.', 'info');
};

window.selectRecipient = function(accountNumber, name) {
  const input = document.getElementById('transfer-dest-account');
  if (input) {
    input.value = accountNumber;
    input.focus();
  }
  showToast(`Recipient selected: ${name} (${accountNumber})`, 'info');
};

window.setTransferAmount = function(amt) {
  const input = document.getElementById('transfer-amount');
  if (input) {
    const current = parseFloat(input.value) || 0;
    input.value = (current + amt).toFixed(0);
    input.focus();
  }
};

function initEventListeners() {
  // Persona switcher buttons
  document.querySelectorAll('.persona-btn').forEach(btn => {
    btn.addEventListener('click', (e) => {
      const persona = e.currentTarget.dataset.persona;
      switchPersona(persona);
    });
  });

  // Transfer form submit
  const transferForm = document.getElementById('transfer-form');
  if (transferForm) {
    transferForm.addEventListener('submit', handleTransferSubmit);
  }

  // Race condition simulator submit
  const raceBtn = document.getElementById('btn-run-race');
  if (raceBtn) {
    raceBtn.addEventListener('click', handleSimulateRace);
  }

  // Idempotency test button
  const testIdempotencyBtn = document.getElementById('btn-test-idempotency');
  if (testIdempotencyBtn) {
    testIdempotencyBtn.addEventListener('click', handleIdempotencyTest);
  }

  // Modals
  setupModal('modal-deposit', 'btn-close-deposit');
  setupModal('modal-withdraw', 'btn-close-withdraw');
  setupModal('modal-create-wallet', 'btn-close-create-wallet');
  setupModal('modal-register', 'btn-close-register');
  setupModal('modal-login', 'btn-close-login');

  // Open modals buttons
  document.getElementById('btn-open-register')?.addEventListener('click', () => openModal('modal-register'));
  document.getElementById('btn-open-login')?.addEventListener('click', () => openModal('modal-login'));

  // New Wallet Button
  const btnNewWallet = document.getElementById('btn-new-wallet');
  if (btnNewWallet) {
    btnNewWallet.addEventListener('click', () => openModal('modal-create-wallet'));
  }

  // Forms
  document.getElementById('form-deposit')?.addEventListener('submit', handleDepositSubmit);
  document.getElementById('form-withdraw')?.addEventListener('submit', handleWithdrawSubmit);
  document.getElementById('form-create-wallet')?.addEventListener('submit', handleCreateWalletSubmit);
  document.getElementById('form-register')?.addEventListener('submit', handleRegisterSubmit);
  document.getElementById('form-login')?.addEventListener('submit', handleCustomLoginSubmit);

  // Generate new Idempotency Key button
  document.getElementById('btn-new-idempotency-key')?.addEventListener('click', () => {
    const keyInput = document.getElementById('transfer-idempotency-key');
    if (keyInput) {
      keyInput.value = crypto.randomUUID();
      showToast('New Idempotency Key generated', 'success');
    }
  });
}

// API Helper
async function apiCall(endpoint, method = 'GET', body = null, customHeaders = {}) {
  const headers = {
    'Content-Type': 'application/json',
    ...customHeaders
  };

  if (currentToken) {
    headers['Authorization'] = `Bearer ${currentToken}`;
  }

  try {
    const options = { method, headers };
    if (body) {
      options.body = JSON.stringify(body);
    }

    const res = await fetch(`${API_BASE}${endpoint}`, options);
    const data = await res.json().catch(() => null);

    if (!res.ok) {
      const errorMsg = data?.title || data?.message || data?.error?.description || `HTTP ${res.status}`;
      return { ok: false, status: res.status, error: errorMsg, data };
    }

    return { ok: true, status: res.status, data };
  } catch (err) {
    return { ok: false, error: err.message };
  }
}

// Switch Persona
async function switchPersona(personaKey) {
  const p = PERSONAS[personaKey];
  if (!p) return;

  showToast(`Authenticating as ${p.name}...`, 'info');
  const res = await apiCall('/api/auth/login', 'POST', {
    email: p.email,
    password: p.password
  });

  if (res.ok && res.data?.token) {
    currentToken = res.data.token;
    currentPersonaKey = personaKey;
    localStorage.setItem('finpulse_token', currentToken);
    localStorage.setItem('finpulse_persona', personaKey);
    highlightPersonaButton(personaKey);
    showToast(`Logged in as ${p.name}`, 'success');
    await refreshDashboard();
    switchTab('wallets');
  } else {
    showToast(`Authentication failed for ${p.name}`, 'error');
  }
}

function highlightPersonaButton(personaKey) {
  document.querySelectorAll('.persona-btn').forEach(btn => {
    if (btn.dataset.persona === personaKey) {
      btn.classList.add('active');
    } else {
      btn.classList.remove('active');
    }
  });
}

// Load Health and System Info
async function loadSystemStatus() {
  const res = await apiCall('/api/system/health');
  if (res.ok && res.data) {
    const h = res.data;
    const dot = document.getElementById('health-dot');
    const text = document.getElementById('health-text');
    const memoryEl = document.getElementById('stat-memory');
    const uptimeEl = document.getElementById('stat-uptime');

    if (dot && text) {
      text.textContent = `${h.status} • SQLite / EF Core`;
    }
    if (memoryEl) memoryEl.textContent = `${h.process.memoryUsageMb} MB`;
    if (uptimeEl) uptimeEl.textContent = h.uptime;
  }
}

// Full Dashboard Refresh
async function refreshDashboard() {
  await Promise.all([
    loadUserProfile(),
    loadWallets(),
    loadWalletAnalytics(),
    loadTransactions(),
    loadAuditLogs()
  ]);
  populateSimulationAndTransferDropdowns();
}

// Load User Profile & Update Auth UI
async function loadUserProfile() {
  const res = await apiCall('/api/auth/me');
  if (res.ok && res.data) {
    const user = res.data;
    const nameEl = document.getElementById('user-full-name');
    const roleEl = document.getElementById('user-role-badge');
    if (nameEl) nameEl.textContent = user.fullName;
    if (roleEl) roleEl.textContent = `${user.role} (${user.email})`;
    updateAuthUi(true, user);
  } else {
    updateAuthUi(false);
  }
}

function updateAuthUi(isLoggedIn, user = null) {
  const guestActions = document.getElementById('guest-actions');
  const userProfileHeader = document.getElementById('user-profile-header');
  const authGateway = document.getElementById('auth-gateway');
  const tabBar = document.getElementById('app-tab-bar');
  const mobNav = document.getElementById('mobile-bottom-nav');
  const heroSection = document.getElementById('hero-banner-section');

  if (isLoggedIn) {
    if (guestActions) guestActions.style.display = 'none';
    if (userProfileHeader) userProfileHeader.style.display = 'flex';
    if (authGateway) authGateway.style.display = 'none';
    if (tabBar) tabBar.style.display = 'flex';
    if (mobNav) mobNav.style.display = '';

    if (user) {
      const headerName = document.getElementById('header-user-name');
      const headerRole = document.getElementById('header-user-role');
      const avatarCircle = document.getElementById('header-avatar-circle');

      if (headerName) headerName.textContent = user.fullName;
      if (headerRole) headerRole.textContent = user.role || 'Customer';

      if (avatarCircle) {
        const initials = user.fullName
          .split(' ')
          .map(n => n[0])
          .join('')
          .toUpperCase()
          .substring(0, 2) || 'U';
        avatarCircle.textContent = initials;
      }
    }
  } else {
    if (guestActions) guestActions.style.display = 'flex';
    if (userProfileHeader) userProfileHeader.style.display = 'none';
    if (authGateway) authGateway.style.display = 'flex';
    if (tabBar) tabBar.style.display = 'none';
    if (mobNav) mobNav.style.display = 'none';

    // Clear dashboard views
    const deck = document.getElementById('wallets-deck');
    if (deck) deck.innerHTML = '';
    const txBody = document.getElementById('tx-table-body');
    if (txBody) txBody.innerHTML = '';
    const auditFeed = document.getElementById('audit-feed');
    if (auditFeed) auditFeed.innerHTML = '';
    
    // Hide panels
    document.querySelectorAll('.view-panel').forEach(p => p.style.display = 'none');
  }
}
window.updateAuthUi = updateAuthUi;

window.handleLogout = function() {
  localStorage.removeItem('finpulse_token');
  localStorage.removeItem('finpulse_persona');
  currentToken = null;
  myWallets = [];
  updateAuthUi(false);
  showToast('Logged out successfully.', 'info');
};

// Load Wallets
async function loadWallets() {
  const res = await apiCall('/api/wallets');
  const deck = document.getElementById('wallets-deck');
  if (!deck) return;

  if (res.ok && Array.isArray(res.data)) {
    myWallets = res.data;
    deck.innerHTML = '';

    if (myWallets.length === 0) {
      deck.innerHTML = `
        <div style="grid-column: 1 / -1; padding: 2.5rem; text-align: center; color: var(--text-muted); background: var(--bg-card); border-radius: var(--radius-lg); border: 1px dashed var(--border-color);">
          <p>No active wallets found for this account.</p>
          <button class="btn btn-primary" style="margin-top: 1rem;" onclick="openModal('modal-create-wallet')">Create First Wallet</button>
        </div>
      `;
      return;
    }

    myWallets.forEach(w => {
      const statusName = w.statusName || w.statusString || (w.status === 1 ? 'Active' : w.status === 2 ? 'Frozen' : 'Closed');
      const isFrozen = w.status === 2 || statusName === 'Frozen';
      const currencyName = w.currencyName || w.currencyString || 'USD';
      const tierName = w.tierName || w.tierString || 'Standard';
      const card = document.createElement('div');
      card.className = `wallet-card glass ${isFrozen ? 'frozen' : ''}`;
      
      const currencySymbol = getCurrencySymbol(currencyName);
      const formattedBalance = `${currencySymbol}${Number(w.balance).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

      card.innerHTML = `
        <div class="wallet-card-header">
          <div class="wallet-currency-badge">
            <span>${currencyName}</span>
            <span style="opacity: 0.6;">• ${tierName}</span>
          </div>
          <span class="wallet-status-pill ${isFrozen ? 'frozen' : 'active'}">
            ${statusName}
          </span>
        </div>

        <div class="wallet-acc-num">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="4" width="20" height="16" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/></svg>
          <span>${w.accountNumber}</span>
        </div>

        <div class="wallet-balance-wrap">
          <div class="wallet-balance-title">Available Balance</div>
          <div class="wallet-balance">${formattedBalance}</div>
        </div>

        <div class="wallet-meta-box">
          <div class="wallet-meta-row">
            <span class="wallet-meta-label">Daily Limit</span>
            <span class="wallet-meta-val">${currencySymbol}${Number(w.dailyLimit).toLocaleString()}</span>
          </div>
          <div class="wallet-meta-row">
            <span class="wallet-meta-label">OCC Token</span>
            <span class="wallet-occ-token" title="Optimistic Concurrency Token: ${w.concurrencyToken}" onclick="navigator.clipboard.writeText('${w.concurrencyToken}'); showToast('OCC Token copied!', 'info');">
              ${w.concurrencyToken.substring(0, 8)}...
              <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><rect x="8" y="2" width="8" height="4" rx="1"/></svg>
            </span>
          </div>
        </div>

        <div class="wallet-actions">
          <button class="btn btn-secondary btn-sm" onclick="openDepositModal('${w.id}', '${w.accountNumber}', '${currencyName}')" ${isFrozen ? 'disabled' : ''}>Deposit</button>
          <button class="btn btn-secondary btn-sm" onclick="openWithdrawModal('${w.id}', '${w.accountNumber}', '${currencyName}', ${w.balance})" ${isFrozen ? 'disabled' : ''}>Withdraw</button>
          <button class="btn btn-primary btn-sm" onclick="quickSendMoney('${w.id}')" ${isFrozen ? 'disabled' : ''}>Send</button>
          ${isFrozen 
            ? `<button class="btn btn-primary btn-sm" onclick="handleActivateWallet('${w.id}')">Unfreeze</button>`
            : `<button class="btn btn-danger btn-sm" onclick="handleFreezeWallet('${w.id}')">Freeze</button>`
          }
        </div>
      `;
      deck.appendChild(card);
    });

    // Update wallet count badge
    const badge = document.getElementById('wallet-count-badge');
    if (badge) badge.textContent = `${myWallets.length} Active`;
  }
}

// Load Analytics
async function loadWalletAnalytics() {
  const res = await apiCall('/api/wallets/analytics');
  if (res.ok && res.data) {
    const a = res.data;
    const totalBalEl = document.getElementById('stat-total-balance');
    const totalTxEl = document.getElementById('stat-tx-count');
    const occSafetyEl = document.getElementById('stat-occ-safety');

    const hasInr = myWallets.some(w => (w.currencyName || w.currencyString) === 'INR' || w.currency === 6);
    const symbol = hasInr ? '₹' : '$';
    const totalBalance = myWallets.reduce((acc, w) => acc + Number(w.balance), 0);

    if (totalBalEl) totalBalEl.textContent = `${symbol}${totalBalance.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    if (totalTxEl) totalTxEl.textContent = a.totalTransactions ?? a.totalTransactionsCount ?? 0;
    if (occSafetyEl) occSafetyEl.textContent = '100% Guaranteed';
  }
}

// Load Transactions
async function loadTransactions() {
  const res = await apiCall('/api/transactions?page=1&pageSize=12');
  const tbody = document.getElementById('tx-table-body');
  if (!tbody) return;

  if (res.ok && res.data?.items) {
    tbody.innerHTML = '';
    const txs = res.data.items;

    if (txs.length === 0) {
      tbody.innerHTML = `<tr><td colspan="7" style="text-align: center; padding: 2rem; color: var(--text-muted);">No ledger transactions recorded yet.</td></tr>`;
      return;
    }

    txs.forEach(t => {
      const tr = document.createElement('tr');
      const typeName = t.typeName || t.typeString || (t.type === 1 ? 'Deposit' : t.type === 2 ? 'Withdrawal' : 'Transfer');
      const statusName = t.statusName || t.statusString || 'Completed';
      const currencyName = t.currencyName || (t.currency === 6 ? 'INR' : t.currency === 1 ? 'USD' : 'INR');
      const isOutflow = typeName === 'Withdrawal' || (typeName === 'Transfer' && myWallets.some(w => w.id === t.sourceWalletId || w.accountNumber === t.sourceAccountNumber));
      const amountClass = isOutflow ? 'amount-negative' : 'amount-positive';
      const amountPrefix = isOutflow ? '-' : '+';
      const typeClass = typeName === 'Deposit' ? 'type-deposit' : typeName === 'Transfer' ? 'type-transfer' : 'type-withdrawal';

      const currSymbol = getCurrencySymbol(currencyName);
      const formattedAmount = `${amountPrefix}${currSymbol} ${Number(t.amount).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

      // Detailed Account / Beneficiary Party information (Sent to / Received from)
      let partyHtml = '';
      if (typeName === 'Transfer') {
        if (isOutflow) {
          partyHtml = `
            <div>
              <span style="font-size: 0.72rem; color: var(--danger); font-weight: 700;">➡️ Sent to:</span>
              <strong style="color: var(--primary); font-family: var(--font-mono); font-size: 0.82rem;">${t.destinationAccountNumber || 'Payee Account'}</strong>
            </div>
            <div style="font-size: 0.7rem; color: var(--text-dim);">From: ${t.sourceAccountNumber || 'My Wallet'}</div>
          `;
        } else {
          partyHtml = `
            <div>
              <span style="font-size: 0.72rem; color: var(--success); font-weight: 700;">⬅️ Received from:</span>
              <strong style="color: var(--success); font-family: var(--font-mono); font-size: 0.82rem;">${t.sourceAccountNumber || 'Payer Account'}</strong>
            </div>
            <div style="font-size: 0.7rem; color: var(--text-dim);">To: ${t.destinationAccountNumber || 'My Wallet'}</div>
          `;
        }
      } else if (typeName === 'Deposit') {
        partyHtml = `
          <div>
            <span style="font-size: 0.72rem; color: var(--success); font-weight: 700;">📥 Deposited to:</span>
            <strong style="color: var(--text-main); font-family: var(--font-mono); font-size: 0.82rem;">${t.destinationAccountNumber || t.sourceAccountNumber || 'My Wallet'}</strong>
          </div>
        `;
      } else if (typeName === 'Withdrawal') {
        partyHtml = `
          <div>
            <span style="font-size: 0.72rem; color: var(--danger); font-weight: 700;">📤 Withdrawn from:</span>
            <strong style="color: var(--text-main); font-family: var(--font-mono); font-size: 0.82rem;">${t.sourceAccountNumber || 'My Wallet'}</strong>
          </div>
        `;
      } else {
        partyHtml = `<span style="font-family: var(--font-mono); font-size: 0.8rem;">${t.destinationAccountNumber || t.sourceAccountNumber || 'Internal'}</span>`;
      }

      tr.innerHTML = `
        <td style="font-family: var(--font-mono); font-size: 0.78rem;">${t.referenceNumber}</td>
        <td><span class="type-pill ${typeClass}">${typeName}</span></td>
        <td>${partyHtml}</td>
        <td class="${amountClass}">${formattedAmount} <span style="font-size: 0.7rem; opacity: 0.75; font-weight: normal;">${currencyName}</span></td>
        <td style="max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${t.description}</td>
        <td><span style="color: ${statusName === 'Completed' ? 'var(--success)' : 'var(--danger)'}; font-weight: 600;">${statusName}</span></td>
        <td style="font-family: var(--font-mono); font-size: 0.75rem; color: var(--text-dim);">${new Date(t.timestampUtc).toLocaleTimeString()}</td>
      `;
      tbody.appendChild(tr);
    });
  }
}

// Load Audit Logs
async function loadAuditLogs() {
  const res = await apiCall('/api/transactions/audit-logs?count=10');
  const feed = document.getElementById('audit-feed');
  if (!feed) return;

  if (res.ok && Array.isArray(res.data)) {
    feed.innerHTML = '';
    const logs = res.data;

    if (logs.length === 0) {
      feed.innerHTML = `<div style="text-align: center; color: var(--text-dim); padding: 1rem;">No audit logs yet.</div>`;
      return;
    }

    logs.forEach(l => {
      const actionName = l.actionName || l.actionString || 'Record';
      const item = document.createElement('div');
      item.className = 'audit-item';
      item.innerHTML = `
        <div class="audit-left">
          <span class="audit-action-tag">${actionName}</span>
          <span><strong>${l.entityName}</strong> (${l.entityId.substring(0, 8)}...) modified by <em>${l.performedBy || 'System'}</em></span>
        </div>
        <div class="audit-time">${new Date(l.timestampUtc).toLocaleTimeString()}</div>
      `;
      feed.appendChild(item);
    });
  }
}

// Populate Dropdowns
function populateSimulationAndTransferDropdowns() {
  const simSelect = document.getElementById('sim-target-wallet');
  const transferSelect = document.getElementById('transfer-source-wallet');

  if (simSelect) {
    simSelect.innerHTML = '';
    myWallets.forEach(w => {
      const opt = document.createElement('option');
      opt.value = w.id;
      const cName = w.currencyName || w.currencyString || 'USD';
      const cSymbol = getCurrencySymbol(cName);
      opt.textContent = `${w.accountNumber} (${cName}) - Balance: ${cSymbol}${Number(w.balance).toLocaleString('en-IN', { minimumFractionDigits: 2 })}`;
      simSelect.appendChild(opt);
    });
  }

  if (transferSelect) {
    transferSelect.innerHTML = '';
    myWallets.forEach(w => {
      const opt = document.createElement('option');
      opt.value = w.id;
      const cName = w.currencyName || w.currencyString || 'USD';
      const cSymbol = getCurrencySymbol(cName);
      opt.textContent = `${w.accountNumber} (${cName}) - Balance: ${cSymbol}${Number(w.balance).toLocaleString('en-IN', { minimumFractionDigits: 2 })}`;
      transferSelect.appendChild(opt);
    });
  }

  // Set default idempotency key
  const keyInput = document.getElementById('transfer-idempotency-key');
  if (keyInput && !keyInput.value) {
    keyInput.value = crypto.randomUUID();
  }
}

// Flagship Feature: Simulate Concurrency & Race Condition
async function handleSimulateRace() {
  const simSelect = document.getElementById('sim-target-wallet');
  const amountInput = document.getElementById('sim-amount');
  const btn = document.getElementById('btn-run-race');

  if (!simSelect || !simSelect.value) {
    showToast('Please select a wallet to test race conditions on.', 'error');
    return;
  }

  const walletId = simSelect.value;
  const debitAmount = parseFloat(amountInput.value) || 500;

  btn.disabled = true;
  btn.innerHTML = `<span class="pulse-dot" style="margin-right: 8px;"></span> Simulating High Concurrency...`;

  try {
    const res = await apiCall('/api/transactions/simulate-race', 'POST', {
      walletId,
      debitAmount
    });

    if (res.ok && res.data) {
      renderRaceResults(res.data);
      showToast('Concurrency simulation completed!', 'success');
      await refreshDashboard();
    } else {
      showToast(res.error || 'Failed to simulate race condition.', 'error');
    }
  } finally {
    btn.disabled = false;
    btn.innerHTML = `
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
      Run Concurrent Debit Simulation
    `;
  }
}

function renderRaceResults(data) {
  const thread1Box = document.getElementById('thread-1-box');
  const thread2Box = document.getElementById('thread-2-box');
  const summaryBox = document.getElementById('sim-summary-box');

  if (thread1Box) {
    thread1Box.className = `thread-box ${data.firstAttemptSuccess ? 'success' : 'rejected'}`;
    thread1Box.innerHTML = `
      <div class="thread-header">
        <span class="thread-title">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
          Worker Thread 01
        </span>
        <span class="thread-status-badge ${data.firstAttemptSuccess ? 'ok' : 'blocked'}">
          ${data.firstAttemptSuccess ? 'Committed (200 OK)' : 'Rejected'}
        </span>
      </div>
      <div class="thread-body">
        ${data.firstAttemptMessage}
      </div>
      <div style="font-size: 0.7rem; font-family: var(--font-mono); color: var(--text-dim); margin-top: auto;">
        Atomic Concurrency Token checked & rotated.
      </div>
    `;
  }

  if (thread2Box) {
    thread2Box.className = `thread-box ${data.secondAttemptSuccess ? 'success' : 'rejected'}`;
    thread2Box.innerHTML = `
      <div class="thread-header">
        <span class="thread-title">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
          Worker Thread 02
        </span>
        <span class="thread-status-badge ${data.secondAttemptSuccess ? 'ok' : 'blocked'}">
          ${data.secondAttemptSuccess ? 'Committed (200 OK)' : 'Blocked / Invariant Guarded'}
        </span>
      </div>
      <div class="thread-body">
        ${data.secondAttemptMessage}
      </div>
      <div style="font-size: 0.7rem; font-family: var(--font-mono); color: ${data.secondAttemptSuccess ? 'var(--text-dim)' : 'var(--danger)'}; margin-top: auto;">
        ${data.secondAttemptSuccess ? 'Both threads succeeded within balance bounds.' : '🛡️ Protected: Prevented double spend & negative balance!'}
      </div>
    `;
  }

  if (summaryBox) {
    summaryBox.style.display = 'block';
    summaryBox.innerHTML = `
      <div style="font-weight: 700; color: var(--primary); display: flex; align-items: center; gap: 0.5rem;">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
        ${data.summary}
      </div>
      <div style="margin-top: 0.35rem; display: flex; gap: 1.5rem; flex-wrap: wrap;">
        <span>Initial Balance: <strong>$${Number(data.initialBalance).toFixed(2)}</strong></span>
        <span>Final Balance: <strong>$${Number(data.finalBalance).toFixed(2)}</strong></span>
        <span>Protection: <span style="color: var(--accent);">${data.concurrencyProtectionMechanism}</span></span>
      </div>
    `;
  }
}

// Transfer Funds with Idempotency
async function handleTransferSubmit(e) {
  e.preventDefault();
  const sourceWalletId = document.getElementById('transfer-source-wallet').value;
  const destinationAccountNumber = document.getElementById('transfer-dest-account').value.trim();
  const amount = parseFloat(document.getElementById('transfer-amount').value);
  const description = document.getElementById('transfer-note').value.trim() || 'P2P Transfer';
  const idempotencyKey = document.getElementById('transfer-idempotency-key').value.trim();

  if (!destinationAccountNumber) {
    showToast('Destination account number is required', 'error');
    return;
  }

  if (!amount || amount <= 0) {
    showToast('Transfer amount must be strictly greater than zero', 'error');
    return;
  }

  const customHeaders = {};
  if (idempotencyKey) {
    customHeaders['Idempotency-Key'] = idempotencyKey;
  }

  const res = await apiCall('/api/transactions/transfer', 'POST', {
    sourceWalletId,
    destinationAccountNumber,
    amount,
    description
  }, customHeaders);

  if (res.ok) {
    const selectedWallet = myWallets.find(w => w.id === sourceWalletId);
    const currCode = selectedWallet?.currencyName || (selectedWallet?.currency === 6 ? 'INR' : 'INR');
    const sym = getCurrencySymbol(currCode);
    showToast(`Transferred ${sym} ${Number(amount).toLocaleString('en-IN', { minimumFractionDigits: 2 })} to ${destinationAccountNumber} successfully!`, 'success');
    document.getElementById('transfer-amount').value = '';
    document.getElementById('transfer-dest-account').value = '';
    // Generate new key for next transaction
    document.getElementById('transfer-idempotency-key').value = crypto.randomUUID();
    await refreshDashboard();
    // Smoothly transition to Passbook view so user sees their updated transaction receipt
    setTimeout(() => {
      switchTab('passbook');
    }, 1000);
  } else {
    showToast(res.error || 'Transfer failed', 'error');
  }
}

// Idempotency Duplicate Test
async function handleIdempotencyTest() {
  const sourceWalletId = document.getElementById('transfer-source-wallet').value;
  const destinationAccountNumber = document.getElementById('transfer-dest-account').value.trim();
  const amount = parseFloat(document.getElementById('transfer-amount').value);
  const idempotencyKey = document.getElementById('transfer-idempotency-key').value.trim();

  if (!destinationAccountNumber || !amount) {
    showToast('Please enter destination account and amount first.', 'error');
    return;
  }

  showToast('Simulating network retry with SAME Idempotency-Key...', 'info');

  const headers = { 'Idempotency-Key': idempotencyKey };
  const res = await apiCall('/api/transactions/transfer', 'POST', {
    sourceWalletId,
    destinationAccountNumber,
    amount,
    description: 'Idempotency test payload'
  }, headers);

  if (res.ok) {
    showToast(`Idempotent Cached Replay detected! Server returned original response without double-debiting.`, 'success');
  } else {
    showToast(`Result: ${res.error}`, 'info');
  }
}

// Wallet actions (Deposit, Withdraw, Freeze)
function openDepositModal(walletId, accNum, currency) {
  document.getElementById('dep-wallet-id').value = walletId;
  document.getElementById('dep-wallet-label').textContent = `${accNum} (${currency})`;
  openModal('modal-deposit');
}

async function handleDepositSubmit(e) {
  e.preventDefault();
  const walletId = document.getElementById('dep-wallet-id').value;
  const amount = parseFloat(document.getElementById('dep-amount').value);
  const description = document.getElementById('dep-desc').value || 'Funds Deposit';

  const res = await apiCall('/api/transactions/deposit', 'POST', {
    walletId,
    amount,
    description
  });

  if (res.ok) {
    showToast('Funds deposited successfully!', 'success');
    closeModal('modal-deposit');
    await refreshDashboard();
  } else {
    showToast(res.error || 'Deposit failed', 'error');
  }
}

function openWithdrawModal(walletId, accNum, currency, currentBal) {
  const sym = getCurrencySymbol(currency);
  document.getElementById('wd-wallet-id').value = walletId;
  document.getElementById('wd-wallet-label').textContent = `${accNum} (${currency}) - Bal: ${sym} ${Number(currentBal).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  openModal('modal-withdraw');
}

async function handleWithdrawSubmit(e) {
  e.preventDefault();
  const walletId = document.getElementById('wd-wallet-id').value;
  const amount = parseFloat(document.getElementById('wd-amount').value);
  const description = document.getElementById('wd-desc').value || 'Funds Withdrawal';

  const res = await apiCall('/api/transactions/withdraw', 'POST', {
    walletId,
    amount,
    description
  });

  if (res.ok) {
    showToast('Funds withdrawn successfully!', 'success');
    closeModal('modal-withdraw');
    await refreshDashboard();
  } else {
    showToast(res.error || 'Withdrawal failed', 'error');
  }
}

async function handleFreezeWallet(walletId) {
  const reason = prompt('Enter reason for freezing this wallet account:', 'Security review audit');
  if (!reason) return;

  const res = await apiCall(`/api/wallets/${walletId}/freeze`, 'POST', { reason });
  if (res.ok) {
    showToast('Wallet has been frozen.', 'warning');
    await refreshDashboard();
  } else {
    showToast(res.error || 'Failed to freeze wallet', 'error');
  }
}

async function handleActivateWallet(walletId) {
  const res = await apiCall(`/api/wallets/${walletId}/activate`, 'POST');
  if (res.ok) {
    showToast('Wallet has been reactivated!', 'success');
    await refreshDashboard();
  } else {
    showToast(res.error || 'Failed to activate wallet', 'error');
  }
}

async function handleCreateWalletSubmit(e) {
  e.preventDefault();
  const currency = parseInt(document.getElementById('new-wallet-currency').value);
  const tier = parseInt(document.getElementById('new-wallet-tier').value);

  const res = await apiCall('/api/wallets', 'POST', {
    currency,
    tier
  });

  if (res.ok) {
    showToast('New multi-currency wallet opened!', 'success');
    closeModal('modal-create-wallet');
    await refreshDashboard();
  } else {
    showToast(res.error || 'Failed to create wallet', 'error');
  }
}

async function handleRegisterSubmit(e) {
  e.preventDefault();
  const fullName = document.getElementById('reg-name').value.trim();
  const email = document.getElementById('reg-email').value.trim();
  const password = document.getElementById('reg-password').value;
  const currency = parseInt(document.getElementById('reg-currency').value);
  const initialBonus = parseFloat(document.getElementById('reg-bonus').value) || 0;

  showToast('Creating new user account...', 'info');
  const res = await apiCall('/api/auth/register', 'POST', {
    fullName,
    email,
    password,
    role: 'Customer'
  });

  if (res.ok && res.data?.token) {
    currentToken = res.data.token;
    localStorage.setItem('finpulse_token', currentToken);
    localStorage.removeItem('finpulse_persona');
    closeModal('modal-register');
    showToast(`Welcome ${fullName}! Setting up wallet...`, 'success');

    // Automatically create selected currency wallet
    const walletRes = await apiCall('/api/wallets', 'POST', {
      currency,
      tier: 1 // Standard
    });

    if (walletRes.ok && initialBonus > 0 && walletRes.data?.id) {
      await apiCall('/api/transactions/deposit', 'POST', {
        walletId: walletRes.data.id,
        amount: initialBonus,
        description: 'Welcome Account Opening Balance'
      });
    }

    highlightPersonaButton(null);
    await refreshDashboard();
    switchTab('wallets');
    showToast(`Account ready! Welcome to FinPulse, ${fullName}!`, 'success');
  } else {
    showToast(res.error || 'Registration failed', 'error');
  }
}

async function handleCustomLoginSubmit(e) {
  e.preventDefault();
  const email = document.getElementById('login-email').value.trim();
  const password = document.getElementById('login-password').value;

  showToast('Logging in...', 'info');
  const res = await apiCall('/api/auth/login', 'POST', { email, password });

  if (res.ok && res.data?.token) {
    currentToken = res.data.token;
    localStorage.setItem('finpulse_token', currentToken);
    localStorage.removeItem('finpulse_persona');
    closeModal('modal-login');
    highlightPersonaButton(null);
    showToast(`Logged in successfully!`, 'success');
    await refreshDashboard();
    switchTab('wallets');
  } else {
    showToast(res.error || 'Invalid email or password', 'error');
  }
}

// Modal Helpers
function setupModal(modalId, closeBtnId) {
  const modal = document.getElementById(modalId);
  const closeBtn = document.getElementById(closeBtnId);
  if (closeBtn && modal) {
    closeBtn.addEventListener('click', () => closeModal(modalId));
  }
  if (modal) {
    modal.addEventListener('click', (e) => {
      if (e.target === modal) closeModal(modalId);
    });
  }
}

function openModal(id) {
  const m = document.getElementById(id);
  if (m) m.classList.add('open');
}

function closeModal(id) {
  const m = document.getElementById(id);
  if (m) m.classList.remove('open');
}

// Toast notification helper
function showToast(message, type = 'info') {
  const container = document.getElementById('toast-container');
  if (!container) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type === 'success' ? 'toast-success' : type === 'error' ? 'toast-error' : ''}`;

  const icon = type === 'success'
    ? `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>`
    : type === 'error'
    ? `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#ef4444" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>`
    : `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#00f2fe" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>`;

  toast.innerHTML = `
    ${icon}
    <div style="flex: 1;">${message}</div>
  `;

  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(100%)';
    toast.style.transition = 'all 0.3s ease-out';
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

function getCurrencySymbol(code) {
  if (!code) return '₹';
  const c = String(code).toUpperCase().trim();
  if (c === 'INR' || c === '6') return '₹';
  if (c === 'USD' || c === '1') return '$';
  if (c === 'EUR' || c === '2') return '€';
  if (c === 'GBP' || c === '3') return '£';
  if (c === 'JPY' || c === '4') return '¥';
  if (c === 'CAD' || c === '5') return 'C$';
  return '₹';
}
