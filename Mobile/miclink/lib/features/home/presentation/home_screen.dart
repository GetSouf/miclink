import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:miclink/app/theme/app_colors.dart';
import 'package:miclink/core/constants/app_constants.dart';
import 'package:miclink/domain/enums/connection_status.dart';
import 'package:miclink/features/home/presentation/controllers/home_controller.dart';
import 'package:miclink/features/home/presentation/widgets/connection_status_card.dart';
import 'package:miclink/features/home/presentation/widgets/mute_button.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  late final HomeController _controller;
  final _pinController = TextEditingController();
  bool _isResetting = false;

  @override
  void initState() {
    super.initState();
    _controller = HomeController();
    _controller.addListener(_onChanged);
  }

  @override
  void dispose() {
    _controller.removeListener(_onChanged);
    _controller.dispose();
    _pinController.dispose();
    super.dispose();
  }

  void _onChanged() => setState(() {});

  Future<void> _connect() async {
    await _controller.pair(_pinController.text);
  }

  Future<void> _resetConnection() async {
    if (_isResetting) {
      return;
    }

    setState(() => _isResetting = true);
    _pinController.clear();
    try {
      await _controller.resetConnection();
    } finally {
      if (mounted) {
        setState(() => _isResetting = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final info = _controller.info;
    final isConnected = _controller.isConnected;
    final showPairing = !isConnected;

    return Scaffold(
      extendBodyBehindAppBar: false,
      appBar: AppBar(
        backgroundColor: AppColors.surface,
        surfaceTintColor: Colors.transparent,
        scrolledUnderElevation: 0,
        centerTitle: true,
        title: Text(AppConstants.appName),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 12),
            child: _ResetButton(
              isResetting: _isResetting,
              onPressed: _resetConnection,
            ),
          ),
        ],
      ),
      body: Container(
            decoration: const BoxDecoration(gradient: AppColors.background),
            child: SafeArea(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
                child: Column(
                  children: [
                    ConnectionStatusCard(info: info),
                    if (isConnected) ...[
                      const SizedBox(height: 12),
                      OutlinedButton.icon(
                        onPressed: _controller.disconnect,
                        icon: const Icon(Icons.link_off_rounded),
                        label: const Text('Отключиться'),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: AppColors.danger,
                          side: const BorderSide(color: AppColors.danger),
                          padding: const EdgeInsets.symmetric(vertical: 12),
                        ),
                      ),
                    ],
                    if (showPairing) ...[
                      const SizedBox(height: 16),
                      _PairingPanel(
                        pinController: _pinController,
                        errorMessage: _controller.errorMessage,
                        onConnect: _connect,
                        pcName: info.pcName,
                        isBusy: _controller.isBusyConnecting,
                        hint: _pairingHint(info.status),
                      ),
                    ],
                    const Spacer(),
                    if (isConnected)
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                        children: [
                          MuteButton(
                            icon: Icons.mic_off_rounded,
                            activeIcon: Icons.mic_rounded,
                            label: 'Микрофон',
                            isMuted: info.isMicrophoneMuted,
                            onPressed: _controller.toggleMicrophone,
                          ),
                          MuteButton(
                            icon: Icons.videocam_off_rounded,
                            activeIcon: Icons.videocam_rounded,
                            label: 'Камера',
                            isMuted: info.isCameraMuted,
                            onPressed: _controller.toggleCamera,
                          ),
                        ],
                      ),
                    const Spacer(),
                  ],
                ),
              ),
            ),
          ),
    );
  }

  String _pairingHint(ConnectionStatus status) => switch (status) {
        ConnectionStatus.reconnecting =>
          'ПК снова в сети — переподключимся автоматически. Или введите PIN.',
        ConnectionStatus.discovering => 'Запустите MicLink на ПК.',
        ConnectionStatus.error => 'Проверьте PIN на экране ПК.',
        _ => 'PIN на экране ПК.',
      };
}

class _ResetButton extends StatelessWidget {
  const _ResetButton({
    required this.isResetting,
    required this.onPressed,
  });

  final bool isResetting;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.danger,
      elevation: 4,
      shadowColor: Colors.black54,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: isResetting ? null : onPressed,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: Colors.white70, width: 1.5),
          ),
          child: isResetting
              ? const SizedBox(
                  width: 24,
                  height: 24,
                  child: CircularProgressIndicator(
                    strokeWidth: 2.5,
                    color: Colors.white,
                  ),
                )
              : const Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.restart_alt_rounded, color: Colors.white, size: 24),
                    SizedBox(width: 6),
                    Text(
                      'Сброс',
                      style: TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                        fontSize: 15,
                      ),
                    ),
                  ],
                ),
        ),
      ),
    );
  }
}

class _PairingPanel extends StatelessWidget {
  const _PairingPanel({
    required this.pinController,
    required this.onConnect,
    required this.hint,
    this.errorMessage,
    this.pcName,
    this.isBusy = false,
  });

  final TextEditingController pinController;
  final VoidCallback onConnect;
  final String hint;
  final String? errorMessage;
  final String? pcName;
  final bool isBusy;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppColors.surface.withValues(alpha: 0.85),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.surfaceLight),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (pcName != null)
            Text(
              pcName!,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
            ),
          const SizedBox(height: 8),
          Text(hint, style: Theme.of(context).textTheme.bodyMedium),
          const SizedBox(height: 12),
          TextField(
            controller: pinController,
            enabled: !isBusy,
            keyboardType: TextInputType.number,
            maxLength: 6,
            textInputAction: TextInputAction.done,
            onSubmitted: (_) {
              if (!isBusy) {
                onConnect();
              }
            },
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: const InputDecoration(
              labelText: 'PIN',
              counterText: '',
              border: OutlineInputBorder(),
            ),
          ),
          if (errorMessage != null) ...[
            const SizedBox(height: 8),
            Text(
              errorMessage!,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppColors.danger,
                  ),
            ),
          ],
          const SizedBox(height: 12),
          FilledButton(
            onPressed: isBusy ? null : onConnect,
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.accent,
              padding: const EdgeInsets.symmetric(vertical: 14),
            ),
            child: isBusy
                ? const SizedBox(
                    height: 22,
                    width: 22,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Подключить'),
          ),
        ],
      ),
    );
  }
}
