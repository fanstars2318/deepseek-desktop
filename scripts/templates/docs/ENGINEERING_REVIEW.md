# 宸ョ▼瀹℃煡娓呭崟锛坉eepseek_publish锛?

鎻愪氦鍚堝苟鍓嶏紝浣滆€呬笌 Reviewer 搴旇鐩栦笅鍒楅」銆侰I 闂ㄧ瑙?`.github/workflows/ci.yml`銆?

## 1. 渚濊禆涓庝緵搴旈摼

- [ ] `dotnet list package --vulnerable` 鏃犳湭鎺ュ彈鐨?Critical/High
- [ ] 杩愯 `scripts/audit-supply-chain.ps1` 骞堕檮缁撴灉
- [ ] 鏂板 NuGet 鍖呭凡纭璁稿彲璇侊紙鍟嗕笟闂簮閬垮厤 GPL 浼犳煋锛?
- [ ] 鏃犲簾寮冦€佹湭浣跨敤鐨?PackageReference

## 2. 浠ｇ爜瑙勮寖

- [ ] 鍛藉悕涓庣幇鏈?`Services/`銆乣DeepSeek.Core/` 椋庢牸涓€鑷?
- [ ] 鏃犺皟璇曠敤 `Console.Write` / 纭紪鐮佸瘑閽?
- [ ] 榄旀硶鏁板瓧宸叉彁鍙栦负鍛藉悕甯搁噺鎴栭厤缃?

## 3. 鏋舵瀯

- [ ] 涓氬姟鐘舵€佷互 `WorkModeCoordinator` / `ConfigStore` 涓哄崟涓€鏁版嵁婧?
- [ ] WebView 娉ㄥ叆浠呴€氳繃 `WebInjectService` 涓?`Assets/inject/*`
- [ ] 鏈湪 WPF 灞傞噸澶嶅疄鐜?Agent 閫昏緫锛堝簲鍦?Core Harness锛?

## 4. 姝ｇ‘鎬т笌杈圭晫

- [ ] 寮傛璺緞鏈夊彇娑堜笌寮傚父浼犳挱
- [ ] WebView 鏈氨缁椂涓嶈皟鐢?`ExecuteScript`
- [ ] 妯″紡鍒囨崲鍦?chat / agent 鍙?WebView 涓婄姸鎬佷竴鑷?

## 5. 瀹夊叏

- [ ] 鏃?Token/瀵嗙爜鍐欏叆鏃ュ織鎴栦粨搴?
- [ ] 鐢ㄦ埛杈撳叆缁?JSON 搴忓垪鍖栵紝鏃犲瓧绗︿覆鎷兼帴鎵ц鍛戒护
- [ ] `appsettings.*.json` 鏈湴瀵嗛挜涓嶅叆搴擄紙瑙?`.gitignore`锛?

## 6. 鎬ц兘涓庤祫婧?

- [ ] `IDisposable` / 杩涚▼閫€鍑烘椂閲婃斁 WebView 涓?MCP 杩炴帴
- [ ] 澶ф枃浠朵笌娴佸紡鍝嶅簲鏈変笂闄愭垨鍙栨秷

## 7. 娴嬭瘯涓庡彲瑙傛祴

- [ ] 鏍稿績閫昏緫鏈?`DeepSeek.Core.Tests` 瑕嗙洊
- [ ] `build.ps1` 閫氳繃 `verify-integration` 涓?harness smoke
- [ ] 鍏抽敭璺緞浣跨敤 `WorkModeTrace` / `AgentDebugLogger` 鑰岄潪涓存椂鏃ュ織

## Review 闂幆

- 姣忔潯 Comment 蹇呴』 **Resolved** 鎴栬鏄庝笉鏀瑰師鍥?
- 绂佹鍦ㄥ瓨鍦ㄦ湭瑙ｅ喅 Comment 鏃跺悎骞?
