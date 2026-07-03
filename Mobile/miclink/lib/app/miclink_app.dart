import 'package:flutter/material.dart';
import 'package:miclink/app/theme/app_theme.dart';
import 'package:miclink/features/home/presentation/home_screen.dart';

class MicLinkApp extends StatelessWidget {
  const MicLinkApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'MicLink',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.dark,
      home: const HomeScreen(),
    );
  }
}
