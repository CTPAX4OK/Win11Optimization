import { motion } from 'framer-motion';
import { Settings, Shield, RefreshCcw, TerminalSquare, Download, Zap } from 'lucide-react';

function App() {
  return (
    <div className="font-sans">
      {/* Background Orbs */}
      <div className="fixed top-[-20%] left-[-10%] w-[50%] h-[50%] bg-[#0078D4] rounded-full blur-[150px] opacity-20 pointer-events-none" />
      <div className="fixed bottom-[-20%] right-[-10%] w-[40%] h-[50%] bg-[#8aadf4] rounded-full blur-[150px] opacity-10 pointer-events-none" />

      {/* Navigation */}
      <nav className="fixed w-full z-50 glass-panel border-x-0 border-t-0 border-b border-white/5">
        <div className="container mx-auto px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Settings className="text-[#8aadf4]" size={24} />
            <span className="font-semibold text-lg tracking-wide text-white">Win11Optimization</span>
          </div>
          <div className="flex gap-4">
            <a href="https://github.com/CTPAX4OK/Win11Optimization" target="_blank" rel="noreferrer" className="flex items-center gap-2 text-sm font-medium text-slate-300 hover:text-white transition-colors">
              <Github size={18} />
              <span>GitHub</span>
            </a>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <section className="pt-32 pb-20 px-6 container mx-auto relative z-10">
        <div className="grid lg:grid-cols-2 gap-16 items-center">
          
          <motion.div 
            initial={{ opacity: 0, x: -30 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ duration: 0.8 }}
          >
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-500/10 border border-blue-500/20 text-blue-400 text-sm font-medium mb-6">
              <Zap size={14} /> v0.1.0 Release
            </div>
            <h1 className="text-5xl lg:text-6xl font-bold leading-tight mb-6 text-white tracking-tight">
              Тонкая настройка <br/>
              <span className="text-transparent bg-clip-text bg-gradient-to-r from-[#8aadf4] to-[#0078D4]">Windows 11</span>
            </h1>
            <p className="text-lg text-slate-400 mb-8 max-w-xl leading-relaxed">
              Модульная и безопасная утилита на C# (.NET 8) для оптимизации системы с возможностью полного отката изменений и создания точек восстановления.
            </p>
            
            <div className="flex flex-wrap gap-4">
              <a 
                href="https://github.com/CTPAX4OK/Win11Optimization/releases" 
                target="_blank" 
                rel="noreferrer"
                className="flex items-center gap-2 bg-[#0078D4] hover:bg-[#006cbd] text-white px-6 py-3 rounded-lg font-medium transition-colors shadow-lg shadow-blue-900/20"
              >
                <Download size={20} />
                Скачать Release
              </a>
              <a 
                href="https://github.com/CTPAX4OK/Win11Optimization" 
                target="_blank" 
                rel="noreferrer"
                className="flex items-center gap-2 glass-button text-white px-6 py-3 rounded-lg font-medium"
              >
                <TerminalSquare size={20} />
                Сборка из исходников
              </a>
            </div>
          </motion.div>

          {/* Console Simulation */}
          <motion.div 
            initial={{ opacity: 0, y: 30, scale: 0.95 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            transition={{ duration: 0.8, delay: 0.2 }}
            className="relative"
          >
            {/* Windows 11 Acrylic styling */}
            <div className="absolute -inset-1 bg-gradient-to-r from-blue-500 to-purple-600 rounded-xl blur opacity-20"></div>
            
            <div className="console-window relative z-10 w-full aspect-[4/3] flex flex-col text-sm">
              <div className="console-header">
                <div className="console-tabs">
                  <div className="console-tab">
                    <TerminalSquare size={14} />
                    Win11Optimization
                  </div>
                </div>
                <div className="console-controls">
                  <div className="w-3 h-3 rounded-full bg-[#555] hover:bg-[#ff5f56] transition-colors cursor-pointer"></div>
                  <div className="w-3 h-3 rounded-full bg-[#555] hover:bg-[#ffbd2e] transition-colors cursor-pointer"></div>
                  <div className="w-3 h-3 rounded-full bg-[#555] hover:bg-[#27c93f] transition-colors cursor-pointer"></div>
                </div>
              </div>
              
              <div className="console-body flex-grow bg-[#0c0c0c]">
                <div className="mb-4">
                  <div className="c-cyan-light font-bold text-lg mb-1">
                    <pre className="leading-tight text-[10px] md:text-xs">
{`__        ___       _ _  ___        _   
\\ \\      / (_)_ __ / / |/ _ \\ _ __ | |_ 
 \\ \\ /\\ / /| | '_ \\| | | | | | '_ \\| __|
  \\ V  V / | | | | | | | |_| | |_) | |_ 
   \\_/\\_/  |_|_| |_|_|_|\\___/| .__/ \\__|
                             |_|        `}
                    </pre>
                  </div>
                  <div className="c-grey mt-2">v0.1.0 | Модульный оптимизатор Windows 11 | MIT License</div>
                </div>

                <div className="border border-[#333] rounded p-3 mb-4 inline-block">
                  <div className="c-cyan-light font-bold mb-1">Система</div>
                  <div><span className="font-bold">ОС:</span>           Windows 11 Pro</div>
                  <div><span className="font-bold">Версия:</span>       10.0.22621</div>
                  <div><span className="font-bold">Сборка:</span>       22621</div>
                  <div><span className="font-bold">Windows 11:</span>   <span className="c-green">Да</span></div>
                  <div><span className="font-bold">Администратор:</span> <span className="c-green">Да</span></div>
                </div>

                <div>
                  <span className="c-green font-bold">[✓]</span> Модули оптимизации: <span className="c-cyan-light">10</span>
                </div>

                <div className="mt-4">
                  <div className="c-cyan-light font-bold">═══ Главное меню ═══</div>
                  <div className="c-cyan-light mt-1">
                    &gt; 📊  Статус оптимизаций<br/>
                    <span className="c-white pl-4">🚀  Применить оптимизации</span><br/>
                    <span className="c-white pl-4">↩️   Откатить оптимизации</span><br/>
                    <span className="c-white pl-4">💾  Создать точку восстановления</span><br/>
                  </div>
                </div>
              </div>
            </div>
          </motion.div>

        </div>
      </section>

      {/* Features Grid */}
      <section className="py-24 relative z-10 bg-black/20 border-y border-white/5">
        <div className="container mx-auto px-6">
          <div className="text-center mb-16">
            <h2 className="text-3xl font-bold text-white mb-4">Разработано для профессионалов</h2>
            <p className="text-slate-400 max-w-2xl mx-auto">
              Никаких скрытых твиков. Вы полностью контролируете, какие модули будут применены, и всегда можете вернуть систему в исходное состояние.
            </p>
          </div>

          <div className="grid md:grid-cols-3 gap-6">
            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              className="glass-panel p-8 rounded-2xl"
            >
              <Shield className="text-[#8aadf4] mb-5" size={32} />
              <h3 className="text-xl font-bold text-white mb-3">Абсолютная безопасность</h3>
              <p className="text-slate-400 text-sm leading-relaxed">
                Автоматическое создание системных точек восстановления перед применением любых изменений. Каждая настройка задокументирована.
              </p>
            </motion.div>

            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: 0.1 }}
              className="glass-panel p-8 rounded-2xl"
            >
              <RefreshCcw className="text-[#8aadf4] mb-5" size={32} />
              <h3 className="text-xl font-bold text-white mb-3">Полный откат (Rollback)</h3>
              <p className="text-slate-400 text-sm leading-relaxed">
                Интеллектуальная система отслеживает старые значения ключей реестра и служб. Вы можете точечно откатить любую оптимизацию.
              </p>
            </motion.div>

            <motion.div 
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: 0.2 }}
              className="glass-panel p-8 rounded-2xl"
            >
              <Settings className="text-[#8aadf4] mb-5" size={32} />
              <h3 className="text-xl font-bold text-white mb-3">Модульная архитектура</h3>
              <p className="text-slate-400 text-sm leading-relaxed">
                Программа написана на современном C# (.NET 8). Легко добавлять свои собственные модули оптимизации через интерфейс IOptimization.
              </p>
            </motion.div>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="py-12 text-center relative z-10 text-slate-500 text-sm">
        <p>&copy; {new Date().getFullYear()} CTPAX4OK. Open-source MIT License.</p>
      </footer>
    </div>
  );
}

function Github({ size = 24 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor" stroke="none">
      <path d="M12 2C6.477 2 2 6.477 2 12c0 4.42 2.865 8.166 6.839 9.489.5.092.682-.217.682-.482 0-.237-.008-.866-.013-1.7-2.782.603-3.369-1.34-3.369-1.34-.454-1.156-1.11-1.462-1.11-1.462-.908-.62.069-.608.069-.608 1.003.07 1.531 1.03 1.531 1.03.892 1.529 2.341 1.087 2.91.831.092-.646.35-1.086.636-1.336-2.22-.253-4.555-1.11-4.555-4.943 0-1.091.39-1.984 1.029-2.683-.103-.253-.446-1.27.098-2.647 0 0 .84-.269 2.75 1.025A9.578 9.578 0 0112 6.836c.85.004 1.705.114 2.504.336 1.909-1.294 2.747-1.025 2.747-1.025.546 1.379.203 2.394.1 2.647.64.699 1.028 1.592 1.028 2.683 0 3.842-2.339 4.687-4.566 4.935.359.309.678.919.678 1.852 0 1.336-.012 2.415-.012 2.743 0 .267.18.578.688.48C19.138 20.161 22 16.416 22 12c0-5.523-4.477-10-10-10z" />
    </svg>
  );
}

export default App;
